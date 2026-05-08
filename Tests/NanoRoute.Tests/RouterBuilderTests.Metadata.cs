/********************************************************************************
* RouterBuilderTests.Metadata.cs                                                *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    internal sealed partial class RouterBuilderTests
    {
        private sealed record TestMetadata(string Value);

        private sealed record OtherTestMetadata(string Value);

        [Test]
        public void Metadata_ShouldStoreValuesByType()
        {
            _routerBuilder.Metadata.Set(new TestMetadata("test"));
            _routerBuilder.Metadata.Set(new OtherTestMetadata("other"));

            Assert.Multiple(() =>
            {
                Assert.That(_routerBuilder.Metadata.TryGet(out TestMetadata? metadata), Is.True);
                Assert.That(metadata, Is.EqualTo(new TestMetadata("test")));

                Assert.That(_routerBuilder.Metadata.TryGet(out OtherTestMetadata? otherMetadata), Is.True);
                Assert.That(otherMetadata, Is.EqualTo(new OtherTestMetadata("other")));
            });
        }

        [Test]
        public void Metadata_ShouldReturnDefaultValueWhenEntryIsMissing()
        {
            TestMetadata metadata = _routerBuilder.Metadata.GetOrDefault(new TestMetadata("default"));

            Assert.That(metadata, Is.EqualTo(new TestMetadata("default")));
        }

        [Test]
        public void Metadata_ShouldInheritParentValuesWhenPrefixIsCreated()
        {
            _routerBuilder.Metadata.Set(new TestMetadata("parent"));

            RouteBuilder childBuilder = _routerBuilder.CreatePrefix("/child/");

            Assert.That(childBuilder.Metadata.TryGet(out TestMetadata? metadata), Is.True);
            Assert.That(metadata, Is.EqualTo(new TestMetadata("parent")));
        }

        [Test]
        public void Metadata_ShouldCreateAnIndependentScopeForPrefixes()
        {
            _routerBuilder.Metadata.Set(new TestMetadata("parent"));

            RouteBuilder childBuilder = _routerBuilder.CreatePrefix("/child/");
            childBuilder.Metadata.Set(new TestMetadata("child"));

            Assert.Multiple(() =>
            {
                Assert.That(_routerBuilder.Metadata.GetOrDefault(new TestMetadata("missing")), Is.EqualTo(new TestMetadata("parent")));
                Assert.That(childBuilder.Metadata.GetOrDefault(new TestMetadata("missing")), Is.EqualTo(new TestMetadata("child")));
            });
        }

        [Test]
        public void Metadata_ShouldNotReflectParentUpdatesMadeAfterPrefixCreation()
        {
            _routerBuilder.Metadata.Set(new TestMetadata("before"));

            RouteBuilder childBuilder = _routerBuilder.CreatePrefix("/child/");
            _routerBuilder.Metadata.Set(new TestMetadata("after"));

            Assert.Multiple(() =>
            {
                Assert.That(_routerBuilder.Metadata.GetOrDefault(new TestMetadata("missing")), Is.EqualTo(new TestMetadata("after")));
                Assert.That(childBuilder.Metadata.GetOrDefault(new TestMetadata("missing")), Is.EqualTo(new TestMetadata("before")));
            });
        }

        [Test]
        public void Metadata_ShouldKeepRemovalsLocalToTheCurrentScope()
        {
            _routerBuilder.Metadata.Set(new TestMetadata("parent"));

            RouteBuilder childBuilder = _routerBuilder.CreatePrefix("/child/");

            Assert.That(childBuilder.Metadata.Remove<TestMetadata>(), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(childBuilder.Metadata.TryGet(out TestMetadata? _), Is.False);
                Assert.That(_routerBuilder.Metadata.TryGet(out TestMetadata? metadata), Is.True);
                Assert.That(metadata, Is.EqualTo(new TestMetadata("parent")));
            });
        }

        [Test]
        public void Metadata_ShouldReplaceExistingValues()
        {
            _routerBuilder.Metadata
                .Set(new TestMetadata("first"));

            _routerBuilder.Metadata
                .Set(new TestMetadata("second"));

            Assert.That(_routerBuilder.Metadata.GetOrDefault(new TestMetadata("missing")), Is.EqualTo(new TestMetadata("second")));
        }

        [Test]
        public void Metadata_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.Metadata.Set<TestMetadata>(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("value"));

            ex = Assert.Throws<ArgumentNullException>(() => _routerBuilder.Metadata.GetOrDefault<TestMetadata>(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("defaultValue"));
        });
    }
}
