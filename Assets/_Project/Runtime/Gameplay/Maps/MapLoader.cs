using System.IO;
using System.Linq;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Maps;

namespace WhoWhoo3D.Gameplay.Maps;

/// <summary>
/// Reads map files from StreamingAssets.
///
/// Split out of GameController so that "where a map comes from" is one decision in one
/// place. The map editor writes here, the game reads from here, and swapping the source
/// later (Addressables, a downloaded map, a test fixture) touches this file alone.
/// </summary>
public static class MapLoader
{
    public const string MapsFolder = "Maps";

    public static string[] ListMaps() =>
        Directory.GetFiles(Path.Combine(Application.streamingAssetsPath, MapsFolder), "*.json")
            .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name, System.StringComparer.Ordinal).ToArray();

    public static string PathFor(string mapFileName) =>
        Path.Combine(Application.streamingAssetsPath, MapsFolder, mapFileName + ".json");

    /// <summary>
    /// Loads and validates a map. Throws <see cref="MapDeserializationException"/> if the
    /// file is malformed and <see cref="FileNotFoundException"/> if it is absent — both
    /// are worth failing loudly on, since a game cannot start without a board.
    /// </summary>
    public static BoardModel Load(string mapFileName)
    {
        string path = PathFor(mapFileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Map '{mapFileName}' not found at {path}.", path);

        return MapSerializer.Deserialize(File.ReadAllText(path));
    }
}
