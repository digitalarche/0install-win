﻿/*
 * Copyright 2010 Bastian Eicher
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
using NUnit.Framework;
using ZeroInstall.Model;
using System.IO;

namespace ZeroInstall.Store.Implementation
{
    public class StoreCreation
    {
        [Test]
        public void ShouldAcceptAnExistingPath()
        {
            var path = FindInexistantPath(Path.GetFullPath("test-store"));
            try
            {
                System.IO.Directory.CreateDirectory(path);
                Assert.DoesNotThrow(delegate { new Store(path); }, "Store must instantiate given an existing path");
            }
            finally
            {
                System.IO.Directory.Delete(path, true);
            }
        }

        private static string FindInexistantPath(string preferredPath)
        {
            while (System.IO.Directory.Exists(preferredPath))
            {
                preferredPath = preferredPath + "_";
            }
            return preferredPath;
        }

        [Test]
        public void ShouldRejectInexistantPath()
        {
            var path = FindInexistantPath(Path.GetFullPath("test-store"));
            Assert.Throws<CacheFolderException>(delegate { new Store(path); }, "Store must throw CacheFolderException created with non-existing path");
        }

        [Test]
        public void ShouldProvideDefaultConstructor()
        {
            Assert.DoesNotThrow(delegate { new Store(); }, "Store must be default constructible");
        }
    }
}
