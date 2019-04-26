using UnityEngine;
using UnityEngine.XR.MagicLeap;

namespace Assets.Scripts
{
    public class EyeTracking : MonoBehaviour
    {
        public Material FocusedMaterial, NonFocusedMaterial;
        public GameObject DetailObjectToOpen;

        private Vector3 heading;
        private MeshRenderer meshRenderer;

        [Tooltip("Specifies the target we will orient to. If no target is specified, the main camera will be used.")]
        private Transform targetTransform;
        public Transform TargetTransform {
            get { return targetTransform; }
            set { targetTransform = value; }
        }

        // Start is called before the first frame update
        void Start() 
        {
            if (DetailObjectToOpen != null)
            {
                DetailObjectToOpen.SetActive(false);
            }

            if (TargetTransform == null) {
                TargetTransform = CameraCache.Main.transform;
            }

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
                heading = MLEyes.FixationPoint - TargetTransform.position;

                if (Physics.Raycast(TargetTransform.position, heading, out rayHit, 10.0f)) {
                    meshRenderer.material = rayHit.transform.position == gameObject.transform.position ? FocusedMaterial : NonFocusedMaterial;

                    if (DetailObjectToOpen != null)
                    {
                        var heading = TargetTransform.position + TargetTransform.forward * 2;
                        DetailObjectToOpen.transform.position = heading;
                        DetailObjectToOpen.SetActive(true);
                    }
                } 
                else 
                {
                    meshRenderer.material = NonFocusedMaterial;
                }
            }
        }
    }
}
