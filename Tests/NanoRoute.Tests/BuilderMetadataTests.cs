/********************************************************************************
* BuilderMetadataTests.cs                                                       *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

using NUnit.Framework;

namespace NanoRoute.Tests
{
    [TestFixture]
    internal sealed class BuilderMetadataTests
    {
        private sealed record TestMetadata(string Value);

        private sealed record OtherTestMetadata(string Value);

        private sealed class CloneableMetadata(string value) : ICloneable
        {
            public string Value { get; set; } = value;

            public object Clone() => new CloneableMetadata(Value);

            public override bool Equals(object? obj) => obj is CloneableMetadata other && Value == other.Value;

            public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        }

        private BuilderMetadata _metadata = null!;

        [SetUp]
        public void Setup()
        {
            _metadata = new BuilderMetadata();
        }

        [Test]
        public void Metadata_ShouldStoreValuesByType()
        {
            _metadata.Set(new TestMetadata("test"));
            _metadata.Set(new OtherTestMetadata("other"));

            Assert.Multiple(() =>
            {
                Assert.That(_metadata.TryGet(out TestMetadata? testMetadata), Is.True);
                Assert.That(testMetadata, Is.EqualTo(new TestMetadata("test")));

                Assert.That(_metadata.TryGet(out OtherTestMetadata? otherMetadata), Is.True);
                Assert.That(otherMetadata, Is.EqualTo(new OtherTestMetadata("other")));
            });
        }

        [Test]
        public void Metadata_ShouldReturnDefaultValueWhenEntryIsMissing()
        {
            Assert.That(_metadata.GetOrDefault(new TestMetadata("default")), Is.EqualTo(new TestMetadata("default")));
        }

        [Test]
        public void Metadata_ShouldReturnStoredValueInsteadOfDefault()
        {
            _metadata.Set(new TestMetadata("stored"));

            Assert.That(_metadata.GetOrDefault(new TestMetadata("default")), Is.EqualTo(new TestMetadata("stored")));
        }

        [Test]
        public void Metadata_ShouldReturnFalseWhenEntryIsMissing()
        {
            Assert.That(_metadata.TryGet(out TestMetadata? value), Is.False);
            Assert.That(value, Is.Null);
        }

        [Test]
        public void Metadata_ShouldReplaceExistingValues()
        {
            _metadata.Set(new TestMetadata("first"));
            _metadata.Set(new TestMetadata("second"));

            Assert.That(_metadata.GetOrDefault(new TestMetadata("default")), Is.EqualTo(new TestMetadata("second")));
        }

        [Test]
        public void Metadata_ShouldRemoveValues()
        {
            _metadata.Set(new TestMetadata("stored"));

            Assert.Multiple(() =>
            {
                Assert.That(_metadata.Remove<TestMetadata>(), Is.True);
                Assert.That(_metadata.Remove<TestMetadata>(), Is.False);
                Assert.That(_metadata.TryGet(out TestMetadata? value), Is.False);
                Assert.That(value, Is.Null);
            });
        }

        [Test]
        public void Metadata_ShouldCreateIndependentScopes()
        {
            _metadata.Set(new TestMetadata("parent"));

            BuilderMetadata child = _metadata.CreateScope();

            _metadata.Set(new TestMetadata("updated-parent"));
            child.Set(new OtherTestMetadata("child"));

            Assert.Multiple(() =>
            {
                Assert.That(child.GetOrDefault(new TestMetadata("missing")), Is.EqualTo(new TestMetadata("parent")));
                Assert.That(_metadata.TryGet(out OtherTestMetadata? _), Is.False);
            });
        }

        [Test]
        public void Metadata_ShouldCloneCloneableValuesWhenCreatingScopes()
        {
            CloneableMetadata parentValue = new("parent");

            _metadata.Set(parentValue);

            BuilderMetadata child = _metadata.CreateScope();

            Assert.That(child.TryGet(out CloneableMetadata? childValue), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(childValue, Is.EqualTo(parentValue));
                Assert.That(childValue, Is.Not.SameAs(parentValue));
            });

            parentValue.Value = "updated-parent";

            Assert.That(child.GetOrDefault(new CloneableMetadata("missing")).Value, Is.EqualTo("parent"));
        }

        [Test]
        public void Metadata_ShouldBeNullChecked() => Assert.Multiple(() =>
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _metadata.Set<TestMetadata>(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("value"));

            ex = Assert.Throws<ArgumentNullException>(() => _metadata.GetOrDefault<TestMetadata>(null!))!;
            Assert.That(ex.ParamName, Is.EqualTo("defaultValue"));
        });
    }
}
