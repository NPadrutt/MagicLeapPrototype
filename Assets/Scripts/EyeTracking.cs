using UnityEngine;
using UnityEngine.XR.MagicLeap;

namespace Assets.Scripts
{
    public enum DetailInfoSide
    {
        Left,
        Right,
    }

    public class EyeTracking : MonoBehaviour
    {
        public Material FocusedMaterial, NonFocusedMaterial;
        public GameObject DetailObjectToOpen;

        [Tooltip("Specifies the axis about which the object will rotate.")]
        [SerializeField]
        private DetailInfoSide detailSide = DetailInfoSide.Left;
        public DetailInfoSide DetailSide {
            get { return detailSide; }
            set { detailSide = value; }
        }

        [Tooltip("Specifies the target we will orient to. If no target is specified, the main camera will be used.")]
        private Transform targetTransform;
        public Transform TargetTransform {
            get { return targetTransform; }
            set { targetTransform = value; }
        }

        private Vector3 heading;
        private MeshRenderer meshRenderer;
        private bool isDetailOpen;



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

                    if (rayHit.transform.position == gameObject.transform.position)
                    {
                        meshRenderer.material = FocusedMaterial;

                        if (DetailObjectToOpen != null && !isDetailOpen)
                        {
                            var heading = TargetTransform.position + TargetTransform.forward * 2;
                            heading.x = DetailSide == DetailInfoSide.Left ? -1.1f : 0.7f;
                            heading.y = 0;

                            DetailObjectToOpen.transform.position = heading;
                            DetailObjectToOpen.SetActive(true);
                            isDetailOpen = true;
                        }
                    }
                    else if (isDetailOpen && rayHit.transform.position != DetailObjectToOpen.transform.position)
                    {
                        DetailObjectToOpen.SetActive(false);
                        isDetailOpen = false;
                        meshRenderer.material = NonFocusedMaterial;
                    }
                    else {
                        meshRenderer.material = NonFocusedMaterial;
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
