﻿/*
 * Copyright 2010-2012 Bastian Eicher
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
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Common.Tasks;
using ZeroInstall.Model;
using ZeroInstall.Store.Implementation;

namespace ZeroInstall.Store.Service
{
    /// <summary>
    /// Provides a service to add new entries to a store that requires elevated privileges to write.
    /// </summary>
    /// <remarks>The represented store data is mutable but the class itself is immutable.</remarks>
    public class ServiceStore : DirectoryStore, IEquatable<ServiceStore>
    {
        #region Variables
        /// <summary>Writes to the Windows event log.</summary>
        private readonly EventLog _eventLog;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new store using a specific path to a cache directory.
        /// </summary>
        /// <param name="path">A fully qualified directory path. The directory will be created if it doesn't exist yet.</param>
        /// <param name="eventLog">Writes to the Windows event log.</param>
        /// <exception cref="IOException">Thrown if the directory could not be created or if the underlying filesystem of the user profile can not store file-changed times accurate to the second.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if creating the directory <paramref name="path"/> is not permitted.</exception>
        public ServiceStore(string path, EventLog eventLog) : base(path)
        {
            #region Sanity checks
            if (eventLog == null) throw new ArgumentNullException("eventLog");
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            #endregion

            _eventLog = eventLog;
            eventLog.WriteEntry(string.Format("Using '{0}' as store directory.", path));
        }
        #endregion

        #region Lifetime
        public override object InitializeLifetimeService()
        {
            // Keep remoting object alive indefinitely
            return null;
        }
        #endregion

        //--------------------//

        /// <inheritdoc />
        protected override string GetTempDir()
        {
            var callingUser = WindowsIdentity.GetCurrent().User;

            using (Service.Identity.Impersonate())
            {
                string path = base.GetTempDir();

                // Give the calling user write access
                var acl = Directory.GetAccessControl(path);
                acl.AddAccessRule(new FileSystemAccessRule(callingUser, FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                Directory.SetAccessControl(path, acl);

                return path;
            }
        }

        /// <inheritdoc />
        protected override void VerifyAndAdd(string tempID, ManifestDigest expectedDigest, ITaskHandler handler)
        {
            var callingUser = WindowsIdentity.GetCurrent().User;

            using (Service.Identity.Impersonate())
            {
                // Take write access away from the calling user
                string path = Path.Combine(DirectoryPath, tempID);
                var acl = Directory.GetAccessControl(path);
                acl.RemoveAccessRule(new FileSystemAccessRule(callingUser, FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                Directory.SetAccessControl(path, acl);

                base.VerifyAndAdd(tempID, expectedDigest, handler);
            }
        }

        #region Remove
        /// <inheritdoc />
        public override void Remove(ManifestDigest manifestDigest)
        {
            var callingIdentity = WindowsIdentity.GetCurrent(true);
            if (callingIdentity != null && !new WindowsPrincipal(callingIdentity).IsInRole(WindowsBuiltInRole.Administrator))
                throw new UnauthorizedAccessException("Must be admin to delete.");

            using (Service.Identity.Impersonate())
            {
                try
                {
                    base.Remove(manifestDigest);
                }
                    #region Error handling
                catch (Exception ex)
                {
                    _eventLog.WriteEntry(string.Format("Failed to remove implementation with digest '{0}':\n{1}", manifestDigest, ex), EventLogEntryType.Warning);
                    throw; // Pass on to caller
                }
                #endregion

                _eventLog.WriteEntry(string.Format("Removed implementation with digest '{0}'.", manifestDigest));
            }
        }
        #endregion

        //--------------------//

        #region Conversion
        /// <summary>
        /// Returns the Store in the form "SecureStore: DirectoryPath". Not safe for parsing!
        /// </summary>
        public override string ToString()
        {
            return "SecureStore: " + DirectoryPath;
        }
        #endregion

        #region Equality
        /// <inheritdoc/>
        public bool Equals(ServiceStore other)
        {
            return base.Equals(other);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == typeof(ServiceStore) && Equals((ServiceStore)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
        #endregion
    }
}