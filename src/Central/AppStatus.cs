// Copyright Bastian Eicher et al.
// Licensed under the GNU Lesser Public License

using ZeroInstall.DesktopIntegration;
using ZeroInstall.Store.Model;

namespace ZeroInstall.Central
{
    /// <summary>
    /// Describes the status of an application represented by an <see cref="IAppTile"/>.
    /// </summary>
    /// <seealso cref="IAppTile.Status"/>
    public enum AppStatus
    {
        /// <summary>The state has not been set yet.</summary>
        Unset,

        /// <summary>The application is listed in a <see cref="Catalog"/> but not in the <see cref="AppList"/>.</summary>
        Candidate,

        /// <summary>The application is listed in the <see cref="AppList"/> but <see cref="AppEntry.AccessPoints"/> is <c>null</c>.</summary>
        Added,

        /// <summary>The application is listed in the <see cref="AppList"/> and <see cref="AppEntry.AccessPoints"/> is set.</summary>
        Integrated
    }
}
