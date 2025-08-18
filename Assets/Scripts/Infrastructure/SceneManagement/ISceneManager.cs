using Cysharp.Threading.Tasks;

namespace Infrastructure.SceneManagement
{
    public interface ISceneManager
    {
        UniTask InitializeSceneAsync();
        void RestartScene();
        void LoadScene(string sceneName);
    }
}