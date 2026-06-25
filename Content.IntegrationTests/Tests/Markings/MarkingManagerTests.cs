using System.Collections.Generic;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Markings;

[TestFixture]
[TestOf(typeof(MarkingManager))]
public sealed class MarkingManagerTests
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: markingsGroup
  id: Testing

- type: markingsGroup
  id: TestingOther

- type: markingsGroup
  id: TestingOptionalEyes
  limits:
    enum.HumanoidVisualLayers.Eyes:
      limit: 1
      required: false

- type: markingsGroup
  id: TestingRequiredEyes
  limits:
    enum.HumanoidVisualLayers.Eyes:
      limit: 1
      required: true
      default: [ EyesMarking ]

- type: marking
  id: SingleColorMarking
  bodyPart: Eyes
  sprites: [{ sprite: Mobs/Customization/human_hair.rsi, state: afro }]
  coloring:
    default:
      type:
        !type:EyeColoring

- type: marking
  id: MenOnlyMarking
  bodyPart: Eyes
  sexRestriction: Male
  sprites: [{ sprite: Mobs/Customization/human_hair.rsi, state: afro }]

- type: marking
  id: TestingOnlyMarking
  bodyPart: Eyes
  groupWhitelist: [ Testing ]
  sprites: [{ sprite: Mobs/Customization/human_hair.rsi, state: afro }]

- type: marking
  id: TestingMenOnlyMarking
  bodyPart: Eyes
  sexRestriction: Male
  groupWhitelist: [ Testing ]
  sprites: [{ sprite: Mobs/Customization/human_hair.rsi, state: afro }]

- type: marking
  id: EyesMarking
  bodyPart: Eyes
  sprites: [{ sprite: Mobs/Customization/human_hair.rsi, state: afro }]

- type: marking
  id: ChestMarking
  bodyPart: Chest
  sprites: [{ sprite: Mobs/Customization/human_hair.rsi, state: afro }]
";

    [Test]
    public async Task HumanHeadHasHairMarkings()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();
            var data = markingManager.GetMarkingData("Human");

            Assert.That(data, Does.ContainKey(new ProtoId<OrganCategoryPrototype>("Head")));
            Assert.That(data["Head"].Layers, Does.Contain(HumanoidVisualLayers.Hair));

            var hair = markingManager.MarkingsByLayerAndGroupAndSex(HumanoidVisualLayers.Hair, "Human", Sex.Male);
            Assert.That(hair.Count, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FelinidHeadHasHairMarkings()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();
            var data = markingManager.GetMarkingData("Felinid");

            Assert.That(data, Does.ContainKey(new ProtoId<OrganCategoryPrototype>("Head")));
            Assert.That(data["Head"].Layers, Does.Contain(HumanoidVisualLayers.Hair));

            var hair = markingManager.MarkingsByLayerAndGroupAndSex(HumanoidVisualLayers.Hair, "Felinid", Sex.Male);
            Assert.That(hair.Count, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("Dwarf", "Head", HumanoidVisualLayers.Hair, "Dwarf")]
    [TestCase("Reptilian", "Head", HumanoidVisualLayers.Snout, "Reptilian")]
    [TestCase("Reptilian", "Torso", HumanoidVisualLayers.Tail, "Reptilian")]
    [TestCase("Shadowkin", "Head", HumanoidVisualLayers.HeadTop, "Shadowkin")]
    [TestCase("Shadowkin", "Torso", HumanoidVisualLayers.Tail, "Shadowkin")]
    [TestCase("Diona", "Torso", HumanoidVisualLayers.Overlay, "Diona")]
    [TestCase("SlimePerson", "Head", HumanoidVisualLayers.Hair, "Slime")]
    [TestCase("SlimePerson", "Head", HumanoidVisualLayers.HeadTop, "Slime")]
    [TestCase("SlimePerson", "Torso", HumanoidVisualLayers.Tail, "Slime")]
    [TestCase("Harpy", "Head", HumanoidVisualLayers.HeadTop, "Harpy")]
    [TestCase("Harpy", "Torso", HumanoidVisualLayers.Tail, "Harpy")]
    [TestCase("Harpy", "ArmRight", HumanoidVisualLayers.RArm, "Harpy")]
    [TestCase("Vulpkanin", "Head", HumanoidVisualLayers.Snout, "Vulpkanin")]
    [TestCase("Vulpkanin", "Torso", HumanoidVisualLayers.Tail, "Vulpkanin")]
    [TestCase("Vulpkanin", "HandLeft", HumanoidVisualLayers.LHand, "Vulpkanin")]
    [TestCase("Felinid", "Torso", HumanoidVisualLayers.Tail, "Felinid")]
    [TestCase("Shark", "Torso", HumanoidVisualLayers.Tail, "Shark")]
    [TestCase("Shark", "Head", HumanoidVisualLayers.HeadSide, "Shark")]
    [TestCase("Arachnid", "Torso", HumanoidVisualLayers.Tail, "Arachnid")]
    [TestCase("Arachnid", "Head", HumanoidVisualLayers.HeadSide, "Arachnid")]
    public async Task SpeciesOrganHasExpectedMarkings(string species, string organ, HumanoidVisualLayers layer, string group)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();
            var data = markingManager.GetMarkingData(species);

            Assert.That(data, Does.ContainKey(new ProtoId<OrganCategoryPrototype>(organ)));
            Assert.That(data[organ].Layers, Does.Contain(layer));

            var markings = markingManager.MarkingsByLayerAndGroupAndSex(layer, group, Sex.Male);
            Assert.That(markings.Count, Is.GreaterThan(0));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SlimeTailMarkingsOnlyWhitelisted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();
            var tails = markingManager.MarkingsByLayerAndGroupAndSex(HumanoidVisualLayers.Tail, "Slime", Sex.Male);

            Assert.That(tails, Does.ContainKey("TailSnake"));
            Assert.That(tails, Does.ContainKey("SlimeCatTail"));
            Assert.That(tails, Does.Not.ContainKey("TailSnakeAnimated"));
            Assert.That(tails, Does.Not.ContainKey("TailKitsune"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HairConvesion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();

            var markings = new List<Marking>() { new("HumanHairLongBedhead2", new List<Color>() { Color.Red }) };

            var converted = markingManager.ConvertMarkings(markings, "Human");

            Assert.That(converted, Does.ContainKey(new ProtoId<OrganCategoryPrototype>("Head")));
            Assert.That(converted["Head"], Does.ContainKey(HumanoidVisualLayers.Hair));
            var hairMarkings = converted["Head"][HumanoidVisualLayers.Hair];
            Assert.That(hairMarkings, Has.Count.EqualTo(1));
            Assert.That(hairMarkings[0].MarkingId, Is.EqualTo("HumanHairLongBedhead2"));
            Assert.That(hairMarkings[0].MarkingColors[0], Is.EqualTo(Color.Red));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LimitsFilling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();
            var dict = new Dictionary<HumanoidVisualLayers, List<Marking>>();

            markingManager.EnsureValidLimits(dict, "TestingRequiredEyes", new() { HumanoidVisualLayers.Eyes }, null, null);
            Assert.That(dict, Does.ContainKey(HumanoidVisualLayers.Eyes));
            Assert.That(dict[HumanoidVisualLayers.Eyes], Has.Count.EqualTo(1));
            Assert.That(dict[HumanoidVisualLayers.Eyes][0].MarkingId, Is.EqualTo("EyesMarking"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LimitsTruncations()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();
            var dict = new Dictionary<HumanoidVisualLayers, List<Marking>>()
            {
                [HumanoidVisualLayers.Eyes] = new()
                {
                    new("EyesMarking", 0),
                    new("MenOnlyMarking", 0),
                },
            };

            markingManager.EnsureValidLimits(dict, "TestingOptionalEyes", new() { HumanoidVisualLayers.Eyes }, null, null);
            Assert.That(dict[HumanoidVisualLayers.Eyes], Has.Count.EqualTo(1));
            Assert.That(dict[HumanoidVisualLayers.Eyes][0].MarkingId, Is.EqualTo("MenOnlyMarking"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnsureValidGroupAndSex()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();
            var dictFactory = static () => new Dictionary<HumanoidVisualLayers, List<Marking>>()
            {
                [HumanoidVisualLayers.Eyes] = new()
                {
                    new("MenOnlyMarking", 0),
                    new("TestingOnlyMarking", 0),
                    new("TestingMenOnlyMarking", 0),
                }
            };

            var menMarkings = dictFactory();
            markingManager.EnsureValidGroupAndSex(menMarkings, "TestingOther", Sex.Male);

            Assert.That(menMarkings[HumanoidVisualLayers.Eyes], Has.Count.EqualTo(1));
            Assert.That(menMarkings[HumanoidVisualLayers.Eyes][0].MarkingId, Is.EqualTo("MenOnlyMarking"));

            var testingMarkings = dictFactory();
            markingManager.EnsureValidGroupAndSex(testingMarkings, "Testing", Sex.Female);

            Assert.That(testingMarkings[HumanoidVisualLayers.Eyes], Has.Count.EqualTo(1));
            Assert.That(testingMarkings[HumanoidVisualLayers.Eyes][0].MarkingId, Is.EqualTo("TestingOnlyMarking"));

            var testingMenMarkings = dictFactory();
            markingManager.EnsureValidGroupAndSex(testingMenMarkings, "Testing", Sex.Male);

            Assert.That(testingMenMarkings[HumanoidVisualLayers.Eyes], Has.Count.EqualTo(3));
            Assert.That(testingMenMarkings[HumanoidVisualLayers.Eyes][0].MarkingId, Is.EqualTo("MenOnlyMarking"));
            Assert.That(testingMenMarkings[HumanoidVisualLayers.Eyes][1].MarkingId, Is.EqualTo("TestingOnlyMarking"));
            Assert.That(testingMenMarkings[HumanoidVisualLayers.Eyes][2].MarkingId, Is.EqualTo("TestingMenOnlyMarking"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EnsureValidColors()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var markingManager = server.ResolveDependency<MarkingManager>();

            var dict = new Dictionary<HumanoidVisualLayers, List<Marking>>()
            {
                [HumanoidVisualLayers.Eyes] = new()
                {
                    new("SingleColorMarking", 0),
                    new("SingleColorMarking", new List<Color>() { Color.Red }),
                    new("SingleColorMarking", 2),
                    new("SingleColorMarking", new List<Color>() { Color.Green }),
                }
            };

            markingManager.EnsureValidColors(dict);

            var eyeMarkings = dict[HumanoidVisualLayers.Eyes];

            // ensure all colors are the correct length
            Assert.That(eyeMarkings[0].MarkingColors, Has.Count.EqualTo(1));
            Assert.That(eyeMarkings[1].MarkingColors, Has.Count.EqualTo(1));
            Assert.That(eyeMarkings[2].MarkingColors, Has.Count.EqualTo(1));
            Assert.That(eyeMarkings[3].MarkingColors, Has.Count.EqualTo(1));

            // and make sure we didn't shuffle our colors around
            Assert.That(eyeMarkings[1].MarkingColors[0], Is.EqualTo(Color.Red));
            Assert.That(eyeMarkings[3].MarkingColors[0], Is.EqualTo(Color.Green));
        });

        await pair.CleanReturnAsync();
    }
}
