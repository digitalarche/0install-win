﻿/*
 * Copyright 2010-2014 Bastian Eicher
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
using System.Drawing;
using System.Windows.Forms;
using Common.Controls;
using Common.Tasks;
using ZeroInstall.Commands.Properties;
using ZeroInstall.Model;
using ZeroInstall.Model.Preferences;
using ZeroInstall.Model.Selection;
using ZeroInstall.Solvers;
using ZeroInstall.Store.Feeds;

namespace ZeroInstall.Commands.WinForms
{
    /// <summary>
    /// Visualizes <see cref="Selections"/> and allows modifications to <see cref="FeedPreferences"/> and <see cref="InterfacePreferences"/>.
    /// </summary>
    public sealed partial class SelectionsControl : UserControl
    {
        #region Variables
        /// <summary>The selections being visualized.</summary>
        private Selections _selections;

        /// <summary>The feed cache used to retrieve feeds for additional information about implementations.</summary>
        private IFeedCache _feedCache;
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new <see cref="Selections"/> control.
        /// </summary>
        public SelectionsControl()
        {
            InitializeComponent();
            CreateHandle();
        }
        #endregion

        //--------------------//

        #region Selections
        /// <summary>
        /// Shows the user the <see cref="Selections"/> made by the <see cref="ISolver"/>.
        /// </summary>
        /// <param name="selections">The <see cref="Selections"/> as provided by the <see cref="ISolver"/>.</param>
        /// <param name="feedCache">The feed cache used to retrieve feeds for additional information about implementations.</param>
        /// <remarks>
        ///   <para>This method must not be called from a background thread.</para>
        ///   <para>This method must not be called before <see cref="Control.Handle"/> has been created.</para>
        /// </remarks>
        public void SetSelections(Selections selections, IFeedCache feedCache)
        {
            #region Sanity checks
            if (selections == null) throw new ArgumentNullException("selections");
            if (InvokeRequired) throw new InvalidOperationException("Method called from a non UI thread.");
            #endregion

            _selections = selections;
            _feedCache = feedCache;

            BuildTable();
            if (_solveCallback != null) CreateLinkLabels();
        }

        private void BuildTable()
        {
            tableLayout.Controls.Clear();
            tableLayout.RowStyles.Clear();

            tableLayout.RowCount = _selections.Implementations.Count;
            for (int i = 0; i < _selections.Implementations.Count; i++)
            {
                // Lines have a fixed height but a variable width
                tableLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54 * AutoScaleDimensions.Height / 13F));

                // Get feed for each selected implementation
                var implementation = _selections.Implementations[i];
                string feedID = (!string.IsNullOrEmpty(implementation.FromFeed) && _feedCache.Contains(implementation.FromFeed))
                    ? implementation.FromFeed
                    : implementation.InterfaceID;
                var feed = _feedCache.GetFeed(feedID);

                // Display application name and implementation version
                tableLayout.Controls.Add(new Label {Text = feed.Name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft}, 0, i);
                tableLayout.Controls.Add(new Label {Text = implementation.Version.ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft}, 1, i);
            }
        }
        #endregion

        #region Modify selections
        /// <summary>
        /// Called after preferences have been changed and the <see cref="ISolver"/> needs to be rerun.
        /// Is set between <see cref="BeginModifySelections"/> and <see cref="EndModifySelections"/>; is <see langword="null"/> otherwise.
        /// </summary>
        private Func<Selections> _solveCallback;

        /// <summary>
        /// Allows the user to modify the <see cref="InterfacePreferences"/> and rerun the <see cref="ISolver"/> if desired.
        /// </summary>
        /// <param name="solveCallback">Called after preferences have been changed and the <see cref="ISolver"/> needs to be rerun.</param>
        /// <remarks>
        ///   <para>This method must not be called from a background thread.</para>
        ///   <para>This method must not be called before <see cref="Control.Handle"/> has been created.</para>
        /// </remarks>
        public void BeginModifySelections(Func<Selections> solveCallback)
        {
            #region Sanity checks
            if (solveCallback == null) throw new ArgumentNullException("solveCallback");
            if (InvokeRequired) throw new InvalidOperationException("Method called from a non UI thread.");
            #endregion

            _solveCallback = solveCallback;
            CreateLinkLabels();
        }

        private void CreateLinkLabels()
        {
            for (int i = 0; i < _selections.Implementations.Count; i++)
                tableLayout.Controls.Add(CreateLinkLabel(_selections.Implementations[i].InterfaceID), 2, i);
        }

        private LinkLabel CreateLinkLabel(string interfaceID)
        {
            var linkLabel = new LinkLabel {Text = Resources.Change, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft};
            linkLabel.LinkClicked += delegate { InterfaceDialog.Show(this, interfaceID, _solveCallback, _feedCache); };
            return linkLabel;
        }

        /// <summary>
        /// Removes the additional UI added by <see cref="BeginModifySelections"/>.
        /// </summary>
        public void EndModifySelections()
        {
            _solveCallback = null;
            BuildTable();
        }
        #endregion

        #region Task tracking
        /// <summary>A list of all <see cref="TrackingControl"/>s used by <see cref="TrackTask"/>. Adressable by associated <see cref="Implementation"/> via <see cref="ManifestDigest"/>.</summary>
        private readonly Dictionary<ManifestDigest, TrackingControl> _trackingControls = new Dictionary<ManifestDigest, TrackingControl>();

        /// <summary>
        /// Registers an <see cref="ITask"/> for a specific implementation for tracking.
        /// </summary>
        /// <param name="task">The task to be tracked. May or may not alreay be running.</param>
        /// <param name="tag">A digest used to associate the <paramref name="task"/> with a specific implementation.</param>
        /// <remarks>
        ///   <para>This method must not be called from a background thread.</para>
        ///   <para>This method must not be called before <see cref="Control.Handle"/> has been created.</para>
        /// </remarks>
        public void TrackTask(ITask task, ManifestDigest tag)
        {
            #region Sanity checks
            if (task == null) throw new ArgumentNullException("task");
            if (InvokeRequired) throw new InvalidOperationException("Method called from a non UI thread.");
            #endregion

            for (int i = 0; i < _selections.Implementations.Count; i++)
            {
                // Locate the row for the implementation the task is associated to
                var implementation = _selections.Implementations[i];
                if (implementation.ManifestDigest.PartialEquals(tag))
                {
                    // Try to find an existing tracking control
                    TrackingControl trackingControl;
                    if (!_trackingControls.TryGetValue(implementation.ManifestDigest, out trackingControl))
                    {
                        // Create a new tracking control if none exists
                        trackingControl = new TrackingControl {Dock = DockStyle.Fill};
                        trackingControl.CreateGraphics(); // Ensure control initialization even in tray icon mode
                        _trackingControls.Add(implementation.ManifestDigest, trackingControl);
                        tableLayout.Controls.Add(trackingControl, 2, i);
                    }

                    // Start tracking the task
                    trackingControl.Task = task;
                }
            }
        }

        /// <summary>
        /// Stops tracking <see cref="ITask"/>s.
        /// </summary>
        public void StopTracking()
        {
            foreach (var trackingControl in _trackingControls.Values)
                trackingControl.Task = null;
        }
        #endregion
    }
}
