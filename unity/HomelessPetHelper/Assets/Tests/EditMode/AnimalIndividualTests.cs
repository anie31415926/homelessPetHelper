using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Animals;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class AnimalIndividualTests
    {
        [Test]
        public void LatestDispositionPreservesIdentityHealthAndFrozenSnapshots()
        {
            var animal = CreateAnimal();
            AnimalSnapshot initial = animal.Current;
            var next = new AnimalDisposition(false, AnimalEmotion.Watchful, AnimalStress.Acute,
                AnimalBehavior.Retreating);
            Assert.That(animal.TryApplyDisposition(initial, next), Is.True);
            Assert.That(animal.Current.AnimalId, Is.EqualTo(initial.AnimalId));
            Assert.That(animal.Current.DefinitionId, Is.EqualTo(initial.DefinitionId));
            Assert.That(animal.Current.Health, Is.SameAs(initial.Health));
            Assert.That(animal.Current.Disposition, Is.SameAs(next));
            Assert.That(initial.Disposition.IsHungry, Is.True);
            Assert.That(initial.Disposition.Stress, Is.EqualTo(AnimalStress.None));
            AnimalSnapshot latest = animal.Current;
            Assert.That(animal.TryApplyDisposition(initial, initial.Disposition), Is.False);
            Assert.That(animal.TryApplyDisposition(null, next), Is.False);
            Assert.That(animal.TryApplyDisposition(latest, null), Is.False);
            Assert.That(animal.TryApplyDisposition(CreateAnimal().Current, next), Is.False);
            Assert.That(animal.Current, Is.SameAs(latest));
        }

        [Test]
        public void HealthCollectionsAreCopiedAndProblemInstancesAreDistinctFromTypes()
        {
            var problems = new List<AnimalHealthProblem>
            {
                new AnimalHealthProblem("problem.one", "test.type"),
                new AnimalHealthProblem("problem.two", "test.type")
            };
            var health = new AnimalHealthSnapshot(problems);
            problems.Clear();
            Assert.That(health.Problems.Count, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<AnimalHealthProblem>)health.Problems).Clear());
            Assert.That(health.Problems[1].InstanceId, Is.EqualTo("problem.two"));
        }

        [Test]
        public void MissingOrDuplicateHealthDataIsNotAnEmptyHealthyRecord()
        {
            Assert.Throws<ArgumentNullException>(() => new AnimalHealthSnapshot(null));
            Assert.Throws<ArgumentException>(() => new AnimalHealthSnapshot(new AnimalHealthProblem[] { null }));
            Assert.Throws<ArgumentException>(() => new AnimalHealthSnapshot(new[]
            {
                new AnimalHealthProblem("same", "test.one"),
                new AnimalHealthProblem("same", "test.two")
            }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" padded ")]
        public void InvalidProblemIdentitiesAreRejected(string id)
        {
            Assert.Throws<ArgumentException>(() => new AnimalHealthProblem(id, "test.type"));
            Assert.Throws<ArgumentException>(() => new AnimalHealthProblem("instance", id));
        }

        [Test]
        public void InvalidIndividualInputsAreRejected()
        {
            var animal = CreateAnimal().Current;
            Assert.Throws<ArgumentException>(() => new AnimalIndividual(Guid.Empty, "cat.shy",
                animal.Disposition, animal.Health));
            Assert.Throws<ArgumentException>(() => new AnimalIndividual(Guid.NewGuid(), " ",
                animal.Disposition, animal.Health));
            Assert.Throws<ArgumentNullException>(() => new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                null, animal.Health));
            Assert.Throws<ArgumentNullException>(() => new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                animal.Disposition, null));
        }

        [TestCase(99, 0, 0)]
        [TestCase(0, 99, 0)]
        [TestCase(0, 0, 99)]
        public void UnknownDispositionCategoriesAreRejected(int emotion, int stress, int behavior)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnimalDisposition(true,
                (AnimalEmotion)emotion, (AnimalStress)stress, (AnimalBehavior)behavior));
        }

        internal static AnimalIndividual CreateAnimal(params AnimalHealthProblem[] problems)
        {
            return new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                new AnimalDisposition(true, AnimalEmotion.Nervous, AnimalStress.None, AnimalBehavior.Watching),
                new AnimalHealthSnapshot(problems));
        }
    }
}
