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
using System.IO;
using System.Net;
using Common;
using ZeroInstall.Injector.Properties;
using ZeroInstall.Store.Feeds;

namespace ZeroInstall.Injector.Feeds
{
    /// <summary>
    /// Provides access to remote and local <see cref="Model.Feed"/>s. Handles downloading, signature verification and caching.
    /// </summary>
    public class FeedManager : IEquatable<FeedManager>, ICloneable
    {
        #region Properties
        /// <summary>
        /// The cache to retreive <see cref="Model.Feed"/>s from and store downloaded <see cref="Model.Feed"/>s to.
        /// </summary>
        public IFeedCache Cache { get; private set; }

        /// <summary>
        /// The OpenPGP-compatible encryption/signature system used to validate new <see cref="Model.Feed"/>s signatures.
        /// </summary>
        public IOpenPgp OpenPgp { get; private set; }

        /// <summary>
        /// Set to <see langword="true"/> to update already cached <see cref="Model.Feed"/>s. 
        /// </summary>
        public bool Refresh { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Creates a new cache based on the given path to a cache directory.
        /// </summary>
        /// <param name="cache">The disk-based cache to store downloaded <see cref="Model.Feed"/>s.</param>
        /// <param name="openPgp">The OpenPGP-compatible encryption/signature system used to validate new <see cref="Model.Feed"/>s signatures.</param>
        public FeedManager(IFeedCache cache, IOpenPgp openPgp)
        {
            #region Sanity checks
            if (cache == null) throw new ArgumentNullException("cache");
            #endregion

            Cache = cache;
            OpenPgp = openPgp;
        }
        #endregion

        //--------------------//

        #region Get feed
        /// <summary>
        /// Returns a specific <see cref="Model.Feed"/>.
        /// </summary>
        /// <param name="feedID">The ID used to identify the feed. May be an HTTP(S) URL or an absolute local path.</param>
        /// <param name="policy">Combines UI access, configuration and resources used to solve dependencies and download implementations.</param>
        /// <param name="stale">Indicates that the returned <see cref="Model.Feed"/> should be updated.</param>
        /// <returns>The parsed <see cref="Model.Feed"/> object.</returns>
        /// <remarks><see cref="Model.Feed"/>s are always served from the <see cref="Cache"/> if possible, unless <see cref="Refresh"/> is set to <see langword="true"/>.</remarks>
        /// <exception cref="Model.InvalidInterfaceIDException">Thrown if <paramref name="feedID"/> is an invalid interface ID.</exception>
        /// <exception cref="IOException">Thrown if a problem occured while reading the feed file.</exception>
        /// <exception cref="WebException">Thrown if a problem occured while fetching the feed file.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read access to the cache is not permitted.</exception>
        public Model.Feed GetFeed(string feedID, Policy policy, out bool stale)
        {
            #region Sanity checks
            if (string.IsNullOrEmpty(feedID)) throw new ArgumentNullException("feedID");
            if (policy == null) throw new ArgumentNullException("policy");
            #endregion

            var preferences = FeedPreferences.LoadForSafe(feedID);

            if (Refresh)
            {
                // ToDo: Download, verify and cache feed
            }

            // Try to load cached feed
            if (Cache.Contains(feedID))
            {
                // Detect when feeds get out-of-date
                TimeSpan feedAge = DateTime.UtcNow - preferences.LastChecked;
                stale = (feedAge > policy.Config.Freshness);

                try { return Cache.GetFeed(feedID); }
                #region Error handling
                catch (InvalidOperationException ex)
                {
                    // Wrap exception since only certain exception types are allowed in tasks
                    throw new IOException(ex.Message, ex);
                }
                #endregion
            }

            if (policy.Config.NetworkUse == NetworkLevel.Offline)
                throw new IOException(string.Format(Resources.FeedNotCachedOffline, feedID));

            // ToDo: Download, verify and cache feed
            throw new IOException(string.Format("The feed '{0}' is not cached. Please run '0install select {0}' first.", feedID));
        }
        #endregion

        //--------------------//

        #region Clone
        /// <summary>
        /// Creates a shallow copy of this <see cref="FeedManager"/> instance.
        /// </summary>
        /// <returns>The new copy of the <see cref="FeedManager"/>.</returns>
        public FeedManager CloneFeedManager()
        {
            return new FeedManager(Cache, OpenPgp) {Refresh = Refresh};
        }
        
        /// <summary>
        /// Creates a shallow copy of this <see cref="FeedManager"/> instance.
        /// </summary>
        /// <returns>The new copy of the <see cref="FeedManager"/>.</returns>
        public object Clone()
        {
            return CloneFeedManager();
        }
        #endregion

        #region Equality
        /// <inheritdoc/>
        public bool Equals(FeedManager other)
        {
            if (other == null) return false;

            return Refresh == other.Refresh && Equals(other.Cache, Cache);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == typeof(FeedManager) && Equals((FeedManager)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Cache != null ? Cache.GetHashCode() : 0) * 397) ^ Refresh.GetHashCode();
            }
        }
        #endregion
    }
}
