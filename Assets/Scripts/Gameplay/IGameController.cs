using Cysharp.Threading.Tasks;

namespace Gameplay
{
    public interface IGameController
    {
        UniTask InitializeGameAsync();
        void StartGame();
        void PauseGame();
        void ResumeGame();
    }
}