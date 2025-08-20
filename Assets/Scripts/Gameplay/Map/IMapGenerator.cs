using Core.HexGrid;
using Cysharp.Threading.Tasks;

namespace Gameplay.Map
{
    public interface IMapGenerator
    {
        UniTask<HexTile[,]> GenerateMapAsync(string mapAssetKey);
    }
}