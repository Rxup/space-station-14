using System.Collections.Generic;
using Content.Server.Cargo.Systems;
using Content.Shared.Research.Prototypes;

namespace Content.IntegrationTests.Tests.Backmen.Lathe;

[TestFixture]
public sealed class LatheRecipyCostTest
{
    private const double Tolerance = 10;

    [Test]
    public async Task LatheRecipesNoArbitrageTest()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var proto = server.ProtoMan;
        var entMan = server.EntMan;
        var priceSystem = entMan.System<PricingSystem>();

        var fails = new List<string>();

        await server.WaitAssertion(() =>
        {
            var recipes = proto.EnumeratePrototypes<LatheRecipePrototype>();
            foreach (var recipe in recipes)
            {
                var resultPrice = Math.Round(priceSystem.GetLatheRecipePrice(recipe));
                var matPrice = 0.0;
                bool ignoreRecipe = false;
                foreach (var (materialId, count) in recipe.Materials)
                {
                    var material = proto.Index(materialId);

                    if (material.IgnoreArbitrage)
                        ignoreRecipe = true;

                    matPrice += material.Price * count;
                }

                if (ignoreRecipe)
                    continue;

                matPrice = Math.Round(matPrice);
                var diff = resultPrice - matPrice;

                if (diff > Tolerance)
                {
                    fails.Add($"ID: {recipe.ID}\n" +
                              $"Materials: {matPrice}\n" +
                              $"Result: {resultPrice}\n" +
                              $"Difference: {diff} (max allowed: {Tolerance})\n" +
                              $"RECOMMENDED FIX: Set result price to ≤ {matPrice + Tolerance}");
                }
            }
        });

        if (fails.Count > 0)
        {
            var msg = "Found arbitrage opportunities:\n\n" +
                      string.Join("\n\n", fails) +
                      "\n\nAdjust lathe recipe prices or material costs.";
            Assert.Fail(msg);
        }

        await pair.CleanReturnAsync();
    }
}
