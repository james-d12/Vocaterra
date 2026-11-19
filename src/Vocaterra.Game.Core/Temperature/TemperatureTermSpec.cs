using System.Text.Json;

namespace Vocaterra.Game.Core.Temperature;

/// <summary>One row of <c>game/temperature.json</c>'s <c>terms</c> (I06c): the term's id and its own parameters, kept as the JSON row until the registry's factory reads them.</summary>
/// <param name="Id">The term's id (the registry key and the term's name).</param>
/// <param name="Row">The whole row, a clone that outlives the parsed document.</param>
public sealed record TemperatureTermSpec(string Id, JsonElement Row);
