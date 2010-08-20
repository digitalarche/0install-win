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
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace ZeroInstall.Model
{
    /// <summary>
    /// Contains test methods for <see cref="Feed"/>.
    /// </summary>
    [TestFixture]
    public class FeedTest
    {
        #region Helpers
        /// <summary>
        /// Creates a fictive test <see cref="Feed"/>.
        /// </summary>
        private static Feed CreateTestFeed()
        {
            return new Feed
            {
                Name = "MyApp",
                Categories = {"Category"},
                Homepage = new Uri("http://0install.net/"),
                Feeds = {new FeedReference {Source = "http://somedomain.net/feed.xml"}},
                FeedFor = {new InterfaceReference {Target = new Uri("http://somedomain.net/interface.xml")}},
                Summaries = {"Default summary", {"German summary", new CultureInfo("de-DE")}},
                Descriptions = {"Default descriptions", {"German descriptions", new CultureInfo("de-DE")}},
                Elements = {CreateTestImplementation(), CreateTestPackageImplementation(), CreateTestGroup()}
            };
        }

        /// <summary>
        /// Creates a fictive test <see cref="Implementation"/>.
        /// </summary>
        private static Implementation CreateTestImplementation()
        {
            return new Implementation
            {
                ID = "id",
                ManifestDigest = new ManifestDigest("sha256=invalid"),
                Version = new ImplementationVersion("1.0"),
                Architecture = new Architecture(OS.Windows, Cpu.I586),
                Languages = {new CultureInfo("en-US")},
                Main = "executable",
                DocDir = "doc",
                Stability = Stability.Developer,
                Dependencies = {new Dependency {Interface = "http://somedomain/interface.xml", Bindings = {new EnvironmentBinding {Name = "PATH"}}}},
                RetrievalMethods = {new Recipe {Steps = {new Archive {Location = new Uri("http://somedomain/archive.zip"), Size = 1024}}}}
            };
        }

        /// <summary>
        /// Creates a fictive test <see cref="PackageImplementation"/>.
        /// </summary>
        private static PackageImplementation CreateTestPackageImplementation()
        {
            return new PackageImplementation
            {
                Package = "firefox",
                Distributions = {"RPM"},
                Version = new ImplementationVersion("1.0"),
                Architecture = new Architecture(OS.Windows, Cpu.I586),
                Languages = {new CultureInfo("en-US")},
                Main = "executable",
                DocDir = "doc",
                Stability = Stability.Developer,
                Dependencies = {new Dependency {Interface = "http://somedomain/interface.xml", Bindings = {new EnvironmentBinding {Name = "PATH"}}}}
            };
        }

        /// <summary>
        /// Creates a fictive test <see cref="Group"/>.
        /// </summary>
        private static Group CreateTestGroup()
        {
            return new Group
            {
                Languages = {"de"},
                Architecture = new Architecture(OS.Solaris, Cpu.I586),
                License = "GPL",
                Stability = Stability.Developer,
                Elements =
                {
                    new Implementation {Main = "main1"},
                    new Group {Elements = {new Implementation {Main = "main2"}}},
                }
            };
        }
        #endregion

        /// <summary>
        /// Ensures that the class is correctly serialized and deserialized.
        /// </summary>
        [Test]
        public void TestSaveLoad()
        {
            Feed feed1, feed2;
            string tempFile = null;
            try
            {
                tempFile = Path.GetTempFileName();

                // Write and read file
                feed1 = CreateTestFeed();
                feed1.Save(tempFile);
                feed2 = Feed.Load(tempFile);
            }
            finally
            { // Clean up
                if (tempFile != null) File.Delete(tempFile);
            }

            // Ensure data stayed the same
            Assert.AreEqual(feed1, feed2, "Serialized objects should be equal.");
            Assert.AreEqual(feed1.GetHashCode(), feed2.GetHashCode(), "Serialized objects' hashes should be equal.");
            Assert.IsFalse(ReferenceEquals(feed1, feed2), "Serialized objects should not return the same reference.");
        }

        /// <summary>
        /// Ensures that the class can be correctly cloned.
        /// </summary>
        [Test]
        public void TestClone()
        {
            var archive1 = CreateTestFeed();
            var archive2 = archive1.CloneFeed();

            // Ensure data stayed the same
            Assert.AreEqual(archive1, archive2, "Cloned objects should be equal.");
            Assert.AreEqual(archive1.GetHashCode(), archive2.GetHashCode(), "Cloned objects' hashes should be equal.");
            Assert.IsFalse(ReferenceEquals(archive1, archive2), "Cloning should not return the same reference.");
        }

        /// <summary>
        /// Ensures that <see cref="Feed.Simplify"/> correctly collapsed <see cref="Group"/> structures.
        /// </summary>
        [Test]
        public void TestSimplify()
        {
            var feed = new Feed { Elements = { CreateTestGroup() } };
            feed.Simplify();

            var implementation = feed.Elements[0];
            Assert.AreEqual(new Architecture(OS.Solaris, Cpu.I586), implementation.Architecture);
            Assert.AreEqual("de", implementation.Languages.ToString());
            Assert.AreEqual("GPL", implementation.License);
            Assert.AreEqual(Stability.Developer, implementation.Stability);
            Assert.AreEqual("main1", implementation.Main);

            implementation = feed.Elements[1];
            Assert.AreEqual(new Architecture(OS.Solaris, Cpu.I586), implementation.Architecture);
            Assert.AreEqual("de", implementation.Languages.ToString());
            Assert.AreEqual("GPL", implementation.License);
            Assert.AreEqual(Stability.Developer, implementation.Stability);
            Assert.AreEqual("main2", implementation.Main);
        }
    }
}
