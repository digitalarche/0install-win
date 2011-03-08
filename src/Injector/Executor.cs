﻿/*
 * Copyright 2010-2011 Bastian Eicher
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Common.Utils;
using ZeroInstall.Injector.Properties;
using ZeroInstall.Model;
using ZeroInstall.Injector.Solver;
using ZeroInstall.Store.Implementation;

namespace ZeroInstall.Injector
{
    /// <summary>
    /// Executes a set of <see cref="ImplementationSelection"/>s as a program using dependency injection.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "C5 collections don't need to be disposed.")]
    public class Executor
    {
        #region Variables
        /// <summary>The specific <see cref="Model.Implementation"/>s chosen for the <see cref="Dependency"/>s.</summary>
        private readonly Selections _selections;

        /// <summary>Used to locate the selected <see cref="Model.Implementation"/>s.</summary>
        private readonly IStore _store;
        #endregion

        #region Properties
        /// <summary>
        /// An alternative executable to to run from the main <see cref="Model.Implementation"/> instead of <see cref="Element.Main"/>.
        /// </summary>
        public string Main { get; set; }

        /// <summary>
        /// Instead of executing the selected program directly, pass it as an argument to this program. Useful for debuggers.
        /// </summary>
        public string Wrapper { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new launcher from <see cref="Selections"/>.
        /// </summary>
        /// <param name="selections">The specific <see cref="ImplementationSelection"/>s chosen for the <see cref="Dependency"/>s.</param>
        /// <param name="store">Used to locate the selected <see cref="Model.Implementation"/>s.</param>
        public Executor(Selections selections, IStore store)
        {
            #region Sanity checks
            if (selections == null) throw new ArgumentNullException("selections");
            if (store == null) throw new ArgumentNullException("store");
            if (selections.Implementations.IsEmpty) throw new ArgumentException(Resources.NoImplementationsPassed, "selections");
            #endregion

            _selections = selections.CloneSelections();
            _store = store;
        }
        #endregion

        //--------------------//

        #region Prepare process
        /// <summary>
        /// Prepares a <see cref="ProcessStartInfo"/> for executing the program as specified by the <see cref="Selections"/>.
        /// </summary>
        /// <param name="arguments">Arguments to be passed to the launched programs.</param>
        /// <returns>The <see cref="ProcessStartInfo"/> that can be used to start the new <see cref="Process"/>.</returns>
        /// <exception cref="ImplementationNotFoundException">Thrown if one of the <see cref="Model.Implementation"/>s is not cached yet.</exception>
        /// <exception cref="CommandException">Thrown if there was a problem locating the implementation executable.</exception>
        public ProcessStartInfo GetStartInfo(string arguments)
        {
            #region Sanity checks
            if (_selections.Commands.IsEmpty) throw new CommandException(Resources.NoCommands);
            #endregion

            var commands = GetCommands();

            // Build command line and add custom wrapper and arguments
            string commandLine = (Wrapper + " " + BuildCommandLine(commands) + arguments).Trim();

            // Prepare the new process to launch the implementation
            ProcessStartInfo startInfo = GetStartInfoHelper(commandLine);

            ApplyBindings(startInfo);

            if (!MonoUtils.IsUnix)
                startInfo.Arguments = StringUtils.ExpandUnixVariables(startInfo.Arguments, startInfo.EnvironmentVariables);

            return startInfo;
        }
        #endregion

        #region Get commands
        /// <summary>
        /// Gets the effective list of commands. Based on <see cref="Selections.Commands"/> but applying possible <see cref="Main"/> overrides.
        /// </summary>
        private IEnumerable<Command> GetCommands()
        {
            var commands = _selections.Commands;

            // Replace first command with custom main if specified
            if (!string.IsNullOrEmpty(Main))
            {
                string mainPath = StringUtils.UnifySlashes(Main);
                mainPath = (mainPath[0] == Path.DirectorySeparatorChar)
                    // Relative to implementation root
                    ? mainPath.TrimStart(Path.DirectorySeparatorChar)
                    // Relative to original command
                    : Path.Combine(Path.GetDirectoryName(_selections.Commands[0].Path) ?? "", mainPath);

                // Clone the list because it needs to be modified (don't want to change original selections)
                commands = (C5.ArrayList<Command>)commands.Clone();

                // Keep the original runner
                commands[0] = new Command {Path = mainPath, Runner = _selections.Commands[0].Runner};
            }

            return commands;
        }
        #endregion

        #region Build command line
        /// <summary>
        /// Builds a complete command-line to execute from a list of commands.
        /// </summary>
        private string BuildCommandLine(IEnumerable<Command> commands)
        {
            string commandLine = "";

            // The firts command always refers to the actual interface to be launched
            string currentRunnerInterface = _selections.InterfaceID;

            foreach (Command command in commands)
            {
                string commandString;
                if (string.IsNullOrEmpty(command.Path))
                { // If the command has no path it can only control its runner using arguments (see below)
                    commandString = "";
                }
                else
                { // The command's path (relative to the interface) and its arguments are combined
                    commandString = GetPath(_selections.GetImplementation(currentRunnerInterface), command);
                    if (!command.Arguments.IsEmpty) commandString += " " + StringUtils.Concatenate(command.Arguments, " ", '"');
                }

                var runner = command.Runner;
                if (runner != null)
                {
                    // Pass additional commands on to runner
                    if (!runner.Arguments.IsEmpty)
                    {
                        // Only prepend whitespace if there is already something there
                        if (!string.IsNullOrEmpty(commandString)) commandString = " " + commandString;

                        commandString = StringUtils.Concatenate(runner.Arguments, " ", '"') + commandString;
                    }

                    // Determine the interface the next command will refer to
                    currentRunnerInterface = runner.Interface;
                }

                // Prepend the string to the command-line
                commandLine = commandString + " " + commandLine;
            }

            return commandLine;
        }
        #endregion

        #region Get StartInfo helper
        /// <summary>
        /// Creates a <see cref="ProcessStartInfo"/> from a command-line.
        /// </summary>
        private static ProcessStartInfo GetStartInfoHelper(string commandLine)
        {
            string fileName;
            string arguments;
            if (commandLine.StartsWith("\""))
            { // File name and arguments separated by first space after closing quotation mark
                string temp = commandLine.Substring(1); // Trim away starting quotation mark
                fileName = StringUtils.GetLeftPartAtFirstOccurrence(temp, "\"");
                arguments = StringUtils.GetRightPartAtFirstOccurrence(temp, "\"").TrimStart();
            }
            else
            { // File name and arguments separated by first space
                fileName = StringUtils.GetLeftPartAtFirstOccurrence(commandLine, ' ');
                arguments = StringUtils.GetRightPartAtFirstOccurrence(commandLine, ' ');
            }

            return new ProcessStartInfo(fileName, arguments) {ErrorDialog = false, UseShellExecute = false};
        }
        #endregion

        #region Path helpers
        /// <summary>
        /// Locates an <see cref="ImplementationBase"/> on the disk (usually in an <see cref="IStore"/>).
        /// </summary>
        /// <param name="implementation">The <see cref="ImplementationBase"/> to be located.</param>
        /// <returns>A fully qualified path pointing to the implementation's location on the local disk.</returns>
        /// <exception cref="ImplementationNotFoundException">Thrown if the <paramref name="implementation"/> is not cached yet.</exception>
        private string GetImplementationPath(ImplementationBase implementation)
        {
            return (string.IsNullOrEmpty(implementation.LocalPath) ? _store.GetPath(implementation.ManifestDigest) : implementation.LocalPath);
        }

        /// <summary>
        /// Gets the fully qualified path of a command inside an implementation.
        /// </summary>
        /// <exception cref="CommandException">Thrown if <paramref name="command"/>'s path is empty, not relative or tries to point outside the implementation directory.</exception>
        private string GetPath(ImplementationSelection implementation, Command command)
        {
            string path = StringUtils.UnifySlashes(command.Path);

            // Fully qualified paths are used by package/native implementatinos
            if (Path.IsPathRooted(path)) return "\"" + path + "\"";

            return "\"" + Path.Combine(GetImplementationPath(implementation), path) + "\"";
        }
        #endregion

        #region Bindings
        /// <summary>
        /// Applies <see cref="Binding"/>s allowing the launched program to locate <see cref="ImplementationSelection"/>s.
        /// </summary>
        /// <param name="startInfo">The program environment to apply the  <see cref="Binding"/>s to.</param>
        /// <exception cref="ImplementationNotFoundException">Thrown if an <see cref="Model.Implementation"/> is not cached yet.</exception>
        private void ApplyBindings(ProcessStartInfo startInfo)
        {
            foreach (var implementation in _selections.Implementations)
            {
                // Apply bindings implementations use to find themselves
                ApplyBindings(startInfo, implementation, implementation);

                // Apply bindings implementations use to find their dependencies
                foreach (var dependency in implementation.Dependencies)
                    ApplyBindings(startInfo, _selections.GetImplementation(dependency.Interface), dependency);
            }

            // The firts command always refers to the actual interface to be launched
            string currentRunnerInterface = _selections.InterfaceID;

            foreach (var command in _selections.Commands)
            {
                // Apply bindings commands use to find their dependencies
                foreach (var dependency in command.Dependencies)
                    ApplyBindings(startInfo, _selections.GetImplementation(dependency.Interface), dependency);

                // Apply bindings commands use to find their runners
                if (command.Runner != null)
                    ApplyBindings(startInfo, _selections.GetImplementation(command.Runner.Interface), command.Runner);

                if (command.WorkingDir != null)
                    ApplyWorkingDir(startInfo, _selections.GetImplementation(currentRunnerInterface), command.WorkingDir);

                // Determine the interface the next command will refer to
                var runner = command.Runner;
                if (runner != null) currentRunnerInterface = runner.Interface;
            }
        }

        /// <summary>
        /// Applies all <see cref="Binding"/>s listed in a specific <see cref="IBindingContainer"/>.
        /// </summary>
        /// <param name="startInfo">The program environment to apply the <see cref="Binding"/>s to.</param>
        /// <param name="implementation">The implementation to be made available via the <see cref="Binding"/>s.</param>
        /// <param name="bindingContainer">The list of <see cref="Binding"/>s to be performed.</param>
        /// <exception cref="ImplementationNotFoundException">Thrown if the <paramref name="implementation"/> is not cached yet.</exception>
        private void ApplyBindings(ProcessStartInfo startInfo, ImplementationSelection implementation, IBindingContainer bindingContainer)
        {
            if (bindingContainer.Bindings.IsEmpty) return;

            // Don't use bindings for PackageImplementations
            if (!string.IsNullOrEmpty(implementation.Package)) return;

            string implementationDirectory = GetImplementationPath(implementation);

            foreach (var binding in bindingContainer.Bindings)
            {
                var environmentBinding = binding as EnvironmentBinding;
                if (environmentBinding != null) ApplyEnvironmentBinding(startInfo, implementationDirectory, environmentBinding);
                //else
                //{
                //    var overlayBinding = binding as OverlayBinding;
                //    if (overlayBinding != null) ApplyOverlayBinding(startInfo, implementationDirectory, overlayBinding);
                //}
            }
        }

        /// <summary>
        /// Applies an <see cref="EnvironmentBinding"/> to the <see cref="ProcessStartInfo"/>.
        /// </summary>
        /// <param name="startInfo">The program environment to apply the <see cref="Binding"/> to.</param>
        /// <param name="implementationDirectory">The implementation to be made available via the <see cref="EnvironmentBinding"/>.</param>
        /// <param name="binding">The binding to apply.</param>
        private static void ApplyEnvironmentBinding(ProcessStartInfo startInfo, string implementationDirectory, EnvironmentBinding binding)
        {
            var environmentVariables = startInfo.EnvironmentVariables;

            string newValue = string.IsNullOrEmpty(binding.Value)
                // A path inside the implementation
                ? Path.Combine(implementationDirectory, StringUtils.UnifySlashes(binding.Insert ?? ""))
                // A static value
                : binding.Value;
            
            // Set the default value if the variable is not already set on the system
            if (!environmentVariables.ContainsKey(binding.Name)) environmentVariables.Add(binding.Name, binding.Default);

            string previousValue = environmentVariables[binding.Name];
            switch (binding.Mode)
            {
                default:
                case EnvironmentMode.Prepend: 
                    environmentVariables[binding.Name] = string.IsNullOrEmpty(previousValue)
                        // No exisiting value, just set new one
                        ? newValue
                        // Prepend new value to existing one seperated by path separator
                        : newValue + Path.PathSeparator + environmentVariables[binding.Name];
                    break;

                case EnvironmentMode.Append:
                    environmentVariables[binding.Name] = string.IsNullOrEmpty(previousValue)
                        // No exisiting value, just set new one
                        ? newValue
                        // Append new value to existing one seperated by path separator
                        : environmentVariables[binding.Name] + Path.PathSeparator + newValue;
                    break;

                case EnvironmentMode.Replace:
                    // Overwrite any existing value
                    environmentVariables[binding.Name] = newValue;
                    break;
            }
        }

        /// <summary>
        /// Applies a <see cref="WorkingDir"/> change to the <see cref="ProcessStartInfo"/>.
        /// </summary>
        /// <param name="startInfo">The program environment to apply the <see cref="WorkingDir"/> change to.</param>
        /// <param name="implementation">The implementation to be made available via the <see cref="WorkingDir"/> change.</param>
        /// <param name="workingDir">The <see cref="WorkingDir"/> to apply.</param>
        /// <exception cref="ImplementationNotFoundException">Thrown if the <paramref name="implementation"/> is not cached yet.</exception>
        /// <exception cref="CommandException">Thrown if the <paramref name="workingDir"/> has an invalid path or another working directory has already been set.</exception>
        /// <remarks>This method can only be called successfully once per <see cref="GetStartInfo"/>.</remarks>
        private void ApplyWorkingDir(ProcessStartInfo startInfo, ImplementationSelection implementation, WorkingDir workingDir)
        {
            string source = StringUtils.UnifySlashes(workingDir.Source) ?? "";
            if (Path.IsPathRooted(source) || source.Contains(".." + Path.DirectorySeparatorChar)) throw new CommandException(Resources.WorkingDirInvalidPath);

            // Only allow working directory to be changed once
            if (!string.IsNullOrEmpty(startInfo.WorkingDirectory)) throw new CommandException(Resources.WokringDirDuplicate);

            startInfo.WorkingDirectory = Path.Combine(GetImplementationPath(implementation), source);
        }
        #endregion
    }
}
