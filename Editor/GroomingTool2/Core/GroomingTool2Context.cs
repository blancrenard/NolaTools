using System;
using GroomingTool2.Managers;
using GroomingTool2.Rendering;
using GroomingTool2.Scene;
using GroomingTool2.Services;
using GroomingTool2.State;
using UnityEngine;

namespace GroomingTool2.Core
{
    internal sealed class GroomingTool2Context : IDisposable
    {
        public GroomingTool2State State { get; private set; }
        public GroomingTool2UI UI { get; private set; }
        public GroomingTool2MaterialManager MaterialManager { get; private set; }
        public GroomingTool2TextureProcessor TextureProcessor { get; private set; }
        public BrushManager BrushManager { get; private set; }
        public VertexSymmetryMapper VertexSymmetryMapper { get; private set; }
        public FurDataManager FurDataManager { get; private set; }
        public FileManager FileManager { get; private set; }
        public GroomingTool2Renderer Renderer { get; private set; }
        public UndoManager UndoManager { get; private set; }
        public CanvasEventHandler EventHandler { get; private set; }
        public State.UvIslandMaskState MaskState { get; private set; }
        public StrokeService StrokeService { get; private set; }
        public BrushStrokeExecutor BrushStrokeExecutor { get; private set; }
        public GroomingSceneViewController SceneViewController { get; private set; }

        public void Initialize()
        {
            // State
            State = GroomingTool2StateRepository.LoadOrCreate();
            if (State != null)
            {
                // State.EnsureAssetFolderExists(); // Handled in Repository
            }

            // Core Services
            TextureProcessor = new GroomingTool2TextureProcessor();
            StrokeService = new StrokeService();
            BrushManager = new BrushManager(StrokeService);
            VertexSymmetryMapper = new VertexSymmetryMapper();
            
            if (State != null)
            {
                FurDataManager = new FurDataManager(BrushManager, StrokeService, State);
            }

            // Managers
            FileManager = new FileManager();
            MaterialManager = new GroomingTool2MaterialManager(TextureProcessor);
            UndoManager = new UndoManager();
            MaskState = new State.UvIslandMaskState(State);

            // UI & Handlers
            if (State != null)
            {
                UI = new GroomingTool2UI(State, MaterialManager, BrushManager, FurDataManager, FileManager, UndoManager, MaskState);
                var maskSelectionHandler = new MaskSelectionHandler(MaskState, FurDataManager, UndoManager, MaterialManager, UI);
                BrushStrokeExecutor = new BrushStrokeExecutor(BrushManager, FurDataManager);
                EventHandler = new CanvasEventHandler(State, BrushManager, FurDataManager, UndoManager, UI, MaskState, MaterialManager, maskSelectionHandler);
            }

            // Renderer
            Renderer = new GroomingTool2Renderer(BrushManager, FurDataManager);
        }

        /// <summary>
        /// Sceneビューコントローラを初期化する（外部からコールバックを設定してから呼ぶ）
        /// </summary>
        public void InitializeSceneViewController(System.Action<string> saveUndoCallback, System.Action repaintCallback)
        {
            if (State == null || MaterialManager == null || FurDataManager == null || BrushStrokeExecutor == null || UI == null || MaskState == null)
                return;

            SceneViewController = new GroomingSceneViewController(
                State,
                MaterialManager,
                FurDataManager,
                BrushStrokeExecutor,
                UI,
                MaskState,
                saveUndoCallback,
                repaintCallback);
        }

        public void Dispose()
        {
            SceneViewController?.Dispose();
            TextureProcessor?.Dispose();
            MaterialManager?.Dispose();
            UI?.Dispose();
            Renderer?.Dispose();
            FurDataManager?.Dispose();
            BrushStrokeExecutor?.Dispose();
            EventHandler?.Dispose();
        }
    }
}
