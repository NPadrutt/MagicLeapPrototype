using System.Diagnostics.Tracing;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

namespace Assets.Scripts {
    public enum DetailInfoSide
    {
        Left,
        Right,
    }

    public class EyeTracking : MonoBehaviour
    {
        public Material FocusedMaterial, NonFocusedMaterial;
        public GameObject DetailObjectToOpen;

        [Tooltip("Specifies the axis about which the object will rotate.")] [SerializeField]
        private DetailInfoSide detailSide = DetailInfoSide.Left;

        public DetailInfoSide DetailSide
        {
            get { return detailSide; }
            set { detailSide = value; }
        }

        [Tooltip("Specifies the target we will orient to. If no target is specified, the main camera will be used.")]
        private Transform targetTransform;

        public Transform TargetTransform
        {
            get { return targetTransform; }
            set { targetTransform = value; }
        }

        private Vector3 heading;
        private MeshRenderer meshRenderer;
        private bool isDetailOpen;
        private int missedCounter;

        // Start is called before the first frame update
        void Start()
        {
            if (TargetTransform == null)
            {
                TargetTransform = CameraCache.Main.transform;
            }

            MLEyes.Start();
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
        }

        void Awake()
        {
            if (DetailObjectToOpen != null) {
                DetailObjectToOpen.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Debug.Log("Eye Tracking Disabled");
            MLEyes.Stop();
            DetailObjectToOpen?.SetActive(false);
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

                if (Physics.Raycast(TargetTransform.position, heading, out rayHit, 10.0f))
                {
                    missedCounter = 0;

                    if (rayHit.collider.gameObject == gameObject)
                    {
                        foreach (var menuItem in GameObject.FindGameObjectsWithTag("MenuItem"))
                        {
                            menuItem.GetComponent<Renderer>().material = NonFocusedMaterial;
                        }

                        meshRenderer.material = FocusedMaterial;

                        if (DetailObjectToOpen != null && !isDetailOpen)
                        {
                            if (detailSide == DetailInfoSide.Left)
                            {
                                heading = gameObject.transform.position + gameObject.transform.right * -0.8f;
                            } else {
                                heading = gameObject.transform.position + gameObject.transform.right * 0.7f;
                            }

                            DetailObjectToOpen.transform.position = heading;
                            DetailObjectToOpen.SetActive(true);
                            isDetailOpen = true;
                        }
                    }
                    else if (rayHit.collider.gameObject == DetailObjectToOpen || rayHit.collider.gameObject.tag == "ReadingArea") 
                    {
                        missedCounter = 0;
                    } 
                    else {
                        DetailObjectToOpen.SetActive(false);
                        isDetailOpen = false;
                        missedCounter = 0;
                        meshRenderer.material = NonFocusedMaterial;
                    }
                }
                else
                {
                    missedCounter++;

                    if (missedCounter >= 30)
                    {
                        DetailObjectToOpen.SetActive(false);
                        isDetailOpen = false;
                        meshRenderer.material = NonFocusedMaterial;
                    }
                }
            }
        }
    }
}