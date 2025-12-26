using System.Collections.Generic;
using GroomingTool2.Core;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace GroomingTool2.Managers
{
    internal sealed class UndoManager
    {
        private readonly Stack<UndoState> undoStack = new();
        private readonly Stack<UndoState> redoStack = new();
        private readonly int maxUndoCount;

        public bool CanUndo => undoStack.Count > 1;
        public bool CanRedo => redoStack.Count > 0;

        public UndoManager(int maxUndoCount = 20)
        {
            this.maxUndoCount = Mathf.Max(1, maxUndoCount);
        }

        public void SaveState(NativeArray<FurData> furData, object _ = null, string description = "")
        {
            SaveState(furData, null, description);
        }

        public void SaveState(NativeArray<FurData> furData, State.UvIslandMaskState maskState, string description = "")
        {
            var state = new UndoState(furData, maskState, description);
            undoStack.Push(state);
            while (undoStack.Count > maxUndoCount)
            {
                undoStack.RemoveBottom();
            }

            redoStack.Clear();
        }

        public UndoState Undo()
        {
            if (!CanUndo)
                return null;

            var current = undoStack.Pop();
            redoStack.Push(current);
            return undoStack.Peek();
        }

        public UndoState Redo()
        {
            if (!CanRedo)
                return null;

            var state = redoStack.Pop();
            undoStack.Push(state);
            return state;
        }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
        }
    }

    internal static class StackExtensions
    {
        public static void RemoveBottom<T>(this Stack<T> stack)
        {
            if (stack.Count == 0)
                return;

            var temp = new Stack<T>(stack);
            var bottomRemoved = false;
            stack.Clear();

            foreach (var item in temp)
            {
                if (!bottomRemoved)
                {
                    bottomRemoved = true;
                    continue;
                }

                stack.Push(item);
            }
        }
    }

    internal sealed class UndoState
    {
        public FurData[,] FurData { get; }
        public bool[,] MaskBaseSelected { get; }
        public bool MaskRestrictEditing { get; }
        public bool HasMaskState { get; }
        public string Description { get; }

        public UndoState(NativeArray<FurData> furData, string description)
            : this(furData, null, description)
        {
        }

        public UndoState(NativeArray<FurData> furData, State.UvIslandMaskState maskState, string description)
        {
            FurData = new FurData[Common.TexSize, Common.TexSize];
            // NativeArray から 2次元配列へコピー
            for (var y = 0; y < Common.TexSize; y++)
            {
                for (var x = 0; x < Common.TexSize; x++)
                {
                    int index = Common.GetIndex(x, y);
                    FurData[x, y] = furData[index];
                }
            }

            if (maskState != null)
            {
                HasMaskState = true;
                MaskBaseSelected = maskState.CloneBaseSelected();
                MaskRestrictEditing = maskState.RestrictEditing;
            }
            else
            {
                HasMaskState = false;
                MaskBaseSelected = null;
                MaskRestrictEditing = false;
            }

            Description = description;
        }
    }
}



