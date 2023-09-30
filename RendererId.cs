using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SubsurfaceStudios.Slightmapper.Global {
    [ExecuteAlways]
    public class RendererId : MonoBehaviour {
        public uint Id;
        public MeshRenderer RendererIfAvailable;
        public ReflectionProbe ReflectionProbeIfAvailable;


        [ContextMenu("Force reregister ID")]
		public void ForceReregisterId() => RendererIdAllocator.RegisterId(Id, this);

		void Awake() => ForceReregisterId();


#if UNITY_EDITOR
        [ContextMenu("Set Renderer")]
        void SetRenderer() => RendererIfAvailable = GetComponent<MeshRenderer>();
        [ContextMenu("Set Reflection Probe")]
        void SetReflectionProbe() => ReflectionProbeIfAvailable = GetComponent<ReflectionProbe>();

        [ContextMenu("Force regenerate ID")]
        public void ForceNewId() {
            Id = RendererIdAllocator.GetId();
            SetRenderer();
            SetReflectionProbe();
            ForceReregisterId();
        }

        public void Reset() => ForceNewId();
#endif // UNITY_EDITOR
    }
}
