#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace NolaTools.FurMaskGenerator.Utils
{
    public static class EditorObjectUtils
    {
        /// <summary>
        /// UnityEngine.Object を安全に破棄する（null チェック付き）
        /// </summary>
        public static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        /// <summary>
        /// 参照を破棄しつつ null に設定する
        /// </summary>
        public static void SafeDestroyAndNullify<T>(ref T obj) where T : UnityEngine.Object
        {
            if (obj != null)
            {
                UnityEngine.Object.DestroyImmediate(obj);
                obj = null;
            }
        }

        /// <summary>
        /// コレクション内の Unity オブジェクトをすべて破棄する（コレクション自体は操作しない）
        /// </summary>
        private static void DestroyObjectsInCollection<T>(IEnumerable<T> collection) where T : UnityEngine.Object
        {
            if (collection == null) return;
            foreach (var obj in collection)
            {
                SafeDestroy(obj);
            }
        }

        /// <summary>
        /// リスト内の Unity オブジェクトをすべて破棄し、リストを Clear する
        /// </summary>
        public static void DestroyAndClearList<T>(IList<T> list) where T : UnityEngine.Object
        {
            if (list == null) return;
            DestroyObjectsInCollection(list);
            list.Clear();
        }

        /// <summary>
        /// Dictionary の値（Unity オブジェクト）をすべて破棄し、辞書を Clear する
        /// </summary>
        public static void DestroyAndClearDictionaryValues<TKey, TValue>(Dictionary<TKey, TValue> dict) where TValue : UnityEngine.Object
        {
            if (dict == null) return;
            DestroyObjectsInCollection(dict.Values);
            dict.Clear();
        }
    }
}
#endif
