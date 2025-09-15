#if UNITY_EDITOR
using UnityEngine;
using Mask.Generator.Constants;

namespace Mask.Generator.Utils
{
    public static class EditorPathUtils
    {
        public static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return null;
            string path = FileConstants.PATH_SEPARATOR + obj.name;
            Transform current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
                path = FileConstants.PATH_SEPARATOR + current.name + path;
            }
            return path;
        }

        // Component/Transform からパスを取得するオーバーロード
        public static string GetGameObjectPath(Component component)
        {
            if (component == null) return null;
            return GetGameObjectPath(component.gameObject);
        }

        public static GameObject FindGameObjectByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // パスが{FileConstants.PATH_SEPARATOR}で始まる場合は除去
            if (path.StartsWith(FileConstants.PATH_SEPARATOR))
                path = path.Substring(1);

            string[] pathParts = path.Split(FileConstants.PATH_SEPARATOR_CHAR);

            // ルートオブジェクトを探す（より限定的な取得）
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var rootObjects = activeScene.GetRootGameObjects();
            GameObject root = null;
            foreach (var obj in rootObjects)
            {
                if (obj != null && obj.name == pathParts[0]) { root = obj; break; }
            }

            if (root == null) return null;

            // 階層を辿る
            Transform current = root.transform;
            for (int i = 1; i < pathParts.Length; i++)
            {
                Transform found = null;
                foreach (Transform child in current)
                {
                    if (child.name == pathParts[i])
                    {
                        found = child;
                        break;
                    }
                }
                if (found == null) return null;
                current = found;
            }

            return current.gameObject;
        }



        // コンポーネント配列からGameObjectパスの一覧を得る汎用関数
        public static System.Collections.Generic.List<string> GetComponentPaths<T>(System.Collections.Generic.IEnumerable<T> components) where T : Component
        {
            var result = new System.Collections.Generic.List<string>();
            if (components == null) return result;
            foreach (var c in components)
            {
                result.Add(GetGameObjectPath(c));
            }
            return result;
        }
    }
}
#endif
