using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SubsurfaceStudios.Slightmapper.Global {
    public class RendererIdAllocator : MonoBehaviour {
        [SerializeField]
        // if you manage to have more than 4,294,967,295 static renderers
        // in one scene you should probably just write your own tool
        private uint AllocatedIds = 0;
        public static uint GetId() => Instance.AllocatedIds++;

        private static RendererIdAllocator s_Instance;
        public static RendererIdAllocator Instance
        {
            get { 
                if (!s_Instance)
                    // Check for an instance in the scene
                    s_Instance = FindObjectOfType<RendererIdAllocator>(true);

                if (!s_Instance) {
                    // No instance in scene? Make a new instance.
                    s_Instance = new GameObject("RENDERER_ID_ALLOC").AddComponent<RendererIdAllocator>();
                    s_Instance.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                }

                return s_Instance; 
            }
        }
        
        public static void RegisterId(uint id, RendererId self) {
            Instance.s_Index ??= new RendererId[Instance.AllocatedIds];

            if (id == Index.Length) {
                RendererId[] newIndex = new RendererId[Index.Length + 10];
                Array.Copy(Index, newIndex, Index.Length);
                Instance.s_Index = newIndex;
            }
            
            Index[id] = self;
        }

        public static RendererId[] Index {
            get => Instance.s_Index;
            set => Instance.s_Index = value;
        }

        public RendererId[] s_Index = null;

        [ContextMenu("Force Reregister all RenderIds")]
        void ForceReregisterAllIds() {
            foreach(var item in FindObjectsOfType<RendererId>(true)) {
                item.ForceReregisterId();
            }
        }
    }
}
