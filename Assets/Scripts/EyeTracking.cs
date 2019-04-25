﻿using UnityEngine;
using UnityEngine.XR.MagicLeap;

namespace Assets.Scripts
{
    public class EyeTracking : MonoBehaviour
    {
        public GameObject Camera;
        public Material FocusedMaterial, NonFocusedMaterial;

        private Vector3 heading;
        private MeshRenderer meshRenderer;

        // Start is called before the first frame update
        void Start()
        {
            MLEyes.Start();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
        }

        private void OnDisable()
        {
            Debug.Log("Eye Tracking Disabled");
            MLEyes.Stop();
        }

        // Update is called once per frame
        void Update()
        {
            if (!MLEyes.IsStarted)
            {
                MLEyes.Start();
                Debug.Log("Eye Tracking reenabled.");
            }

            if (MLEyes.IsStarted)
            {
                RaycastHit rayHit;
                heading = MLEyes.FixationPoint - Camera.transform.position;

                if (Physics.Raycast(Camera.transform.position, heading, out rayHit, 10.0f)) {
                    meshRenderer.material = rayHit.transform.position == gameObject.transform.position ? FocusedMaterial : NonFocusedMaterial;
                } else {
                    meshRenderer.material = NonFocusedMaterial;
                }
            }
        }
    }
}
