using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.MagicLeap;
using ZXing;

namespace Assets.Scripts
{
    public class QrCodeReader : MonoBehaviour
    {
        public TextMeshPro titleMesh;
        public GameObject ContainerGameObject;

        [field: Tooltip("Specifies the target we will orient to. If no target is specified, the main camera will be used.")]
        public Transform TargetTransform { get; set; }

        #region Private Variables

        private bool isCameraConnected;
        private bool isCapturing;

        private MLHandKeyPose[] gestures;

        private enum PrivilegeState
        {
            Off,
            Started,
            Requested,
            Granted,
            Denied
        }

        private PrivilegeState _currentPrivilegeState = PrivilegeState.Off;

        private readonly MLPrivilegeId[] _privilegesNeeded = {MLPrivilegeId.CameraCapture};

        private readonly List<MLPrivilegeId> _privilegesGranted = new List<MLPrivilegeId>();

        private bool _hasStarted;

        #endregion

        #region Unity Methods

        /// <summary>
        ///     Start Privilege API.
        /// </summary>
        private void OnEnable()
        {
            if (TargetTransform == null) {
                TargetTransform = CameraCache.Main.transform;
            }

            ContainerGameObject.SetActive(false);

            var result = MLPrivileges.Start();
            if (result.IsOk)
            {
                _currentPrivilegeState = PrivilegeState.Started;
            }
            else
            {
                Debug.LogError("Privilege Error: failed to startup");
                enabled = false;
            }

            StartHandTracking();
        }

        private void StartHandTracking()
        {
            MLHands.Start(); // Start the hand tracking.

            gestures = new MLHandKeyPose[3]; //Assign the gestures we will look for.
            gestures[0] = MLHandKeyPose.Fist;
            gestures[1] = MLHandKeyPose.OpenHandBack;
            gestures[2] = MLHandKeyPose.Thumb;

            // Enable the hand poses.
            MLHands.KeyPoseManager.EnableKeyPoses(gestures, true, false);
            Debug.LogError("Hand Tracking started");
        }

        /// <summary>
        ///     Stop the camera, unregister callbacks, and stop input and privileges APIs.
        /// </summary>
        private void OnDisable()
        {
            if (MLInput.IsStarted)
            {
                MLInput.OnControllerButtonDown -= OnButtonDown;
                MLInput.Stop();
            }

            if (MLHands.IsStarted)
            {
                MLHands.Stop();
            }

            if (isCameraConnected)
            {
                MLCamera.OnRawImageAvailable -= OnCaptureRawImageComplete;
                isCapturing = false;
                DisableMLCamera();
            }

            if (_currentPrivilegeState != PrivilegeState.Off)
            {
                MLPrivileges.Stop();

                _currentPrivilegeState = PrivilegeState.Off;
                _privilegesGranted.Clear();
            }
        }

        /// <summary>
        ///     Cannot make the assumption that a reality privilege is still granted after
        ///     returning from pause. Return the application to the state where it
        ///     requests privileges needed and clear out the list of already granted
        ///     privileges. Also, disable the camera and unregister callbacks.
        /// </summary>
        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                if (_currentPrivilegeState != PrivilegeState.Off)
                {
                    _privilegesGranted.Clear();
                    _currentPrivilegeState = PrivilegeState.Started;
                }

                if (isCameraConnected)
                {
                    MLCamera.OnRawImageAvailable -= OnCaptureRawImageComplete;
                    isCapturing = false;
                    DisableMLCamera();
                }

                MLInput.OnControllerButtonDown -= OnButtonDown;

                _hasStarted = false;
            }
        }

        /// <summary>
        ///     Move through the privilege stages before enabling the feature that requires privileges.
        /// </summary>
        private void Update()
        {
            // Privileges have not yet been granted, go through the privilege states.
            if (_currentPrivilegeState != PrivilegeState.Granted) UpdatePrivilege();

            if (GetGesture(MLHands.Left, MLHandKeyPose.OpenHandBack) || GetGesture(MLHands.Right, MLHandKeyPose.OpenHandBack))
            {
                Debug.Log("Recognized Open Hand BackHand Pose");
                TriggerHide();
            }

            if (GetGesture(MLHands.Left, MLHandKeyPose.Thumb) || GetGesture(MLHands.Right, MLHandKeyPose.Thumb))
            {
                Debug.Log($"Privilege state: {_currentPrivilegeState}");
                Debug.Log("Recognized Thumb Hand Pose");
                if (_currentPrivilegeState == PrivilegeState.Granted) TriggerAsyncCapture();
            }

            if (GetGesture(MLHands.Left, MLHandKeyPose.Fist) || GetGesture(MLHands.Right, MLHandKeyPose.Fist))
            {
                Debug.Log($"Privilege state: {_currentPrivilegeState}");
                Debug.Log("Recognized Fist Hand Pose");
                if (_currentPrivilegeState == PrivilegeState.Granted) StartCapture();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Connects the MLCamera component and instantiates a new instance
        ///     if it was never created.
        /// </summary>
        public bool EnableMLCamera()
        {
            var result = MLCamera.Start();
            if (result.IsOk)
            {
                result = MLCamera.Connect();
                isCameraConnected = result.IsOk;
            }

            return isCameraConnected;
        }

        /// <summary>
        ///     Disconnects the MLCamera if it was ever created or connected.
        /// </summary>
        public void DisableMLCamera()
        {
            MLCamera.Disconnect();
            // Explicitly set to false here as the disconnect was attempted.
            isCameraConnected = false;
            _hasStarted = false;
            MLCamera.Stop();
        }

        /// <summary>
        ///     Captures a still image using the device's camera and returns
        ///     the data path where it is saved.
        /// </summary>
        public void TriggerAsyncCapture()
        {
            if (MLCamera.IsStarted && isCameraConnected)
            {
                Debug.Log("Capture Triggered.");
                var result = MLCamera.CaptureRawImageAsync();
                if (result.IsOk) isCapturing = true;
            }
        }

        public void TriggerHide()
        {
            ContainerGameObject.SetActive(false);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        ///     Handles the event for button down.
        /// </summary>
        /// <param name="controllerId">The id of the controller.</param>
        /// <param name="button">The button that is being pressed.</param>
        private void OnButtonDown(byte controllerId, MLInputControllerButton button)
        {
            if (MLInputControllerButton.Bumper == button && !isCapturing) TriggerAsyncCapture();
            if (MLInputControllerButton.HomeTap == button && !isCapturing) TriggerHide();
        }

        private void OnCaptureRawImageComplete(byte[] imageData)
        {
            isCapturing = false;

            // Initialize to 8x8 texture so there is no discrepency
            // between uninitalized captures and error texture
            Texture2D texture = new Texture2D(8, 8);
            bool status = texture.LoadImage(imageData);

            ReadQRCode(texture);
        }

        private void ReadQRCode(Texture2D texture)
        {
            Debug.Log("QR Code: VERSION 1");
            var reader = new BarcodeReader();
            var result = reader.Decode(texture.GetPixels32(), texture.width, texture.height);

            if (result != null) 
            {

                Debug.Log("QR Code Decoded...");
                Debug.Log("DECODED TEXT FROM QR: " + result.Text);

                titleMesh.text = result.Text;

                DisableMLCamera();

                ContainerGameObject.SetActive(true);
                
                var heading = TargetTransform.position + TargetTransform.forward * 2;
                ContainerGameObject.transform.position = heading;
            } 
            else
            {
                Debug.Log("No QR Code found");
            }
        }

        #endregion

        #region Private Functions

        bool GetGesture(MLHand hand, MLHandKeyPose type)
        {
            if (hand != null)
            {
                if (hand.KeyPose == type)
                {
                    if (hand.KeyPoseConfidence > 0.9f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        ///     Handle the privilege states.
        /// </summary>
        private void UpdatePrivilege()
        {
            switch (_currentPrivilegeState)
            {
                // Privilege API has been started successfully, ready to make requests.
                case PrivilegeState.Started:
                {
                    RequestPrivileges();
                    break;
                }
                // Privilege requests have been made, wait until all privileges are granted before enabling the feature that requires privileges.
                case PrivilegeState.Requested:
                {
                    foreach (var priv in _privilegesNeeded)
                        if (!_privilegesGranted.Contains(priv))
                            return;
                    _currentPrivilegeState = PrivilegeState.Granted;
                    break;
                }
                // Privileges have been denied, respond appropriately.
                case PrivilegeState.Denied:
                {
                    enabled = false;
                    break;
                }
            }
        }

        /// <summary>
        ///     Once privileges have been granted, enable the camera and callbacks.
        /// </summary>
        private void StartCapture()
        {
            Debug.LogError($"has started: {_hasStarted}");
            if (!_hasStarted)
            {
                var result = MLInput.Start();
                if (!result.IsOk)
                {
                    Debug.LogError("Failed to start MLInput on ImageCapture component. Disabling the script.");
                    enabled = false;
                    return;
                }

                if (!EnableMLCamera())
                {
                    Debug.LogError("MLCamera failed to connect. Disabling ImageCapture component.");
                    enabled = false;
                    return;
                }

                MLInput.OnControllerButtonDown += OnButtonDown;
                MLCamera.OnRawImageAvailable += OnCaptureRawImageComplete;

                _hasStarted = true;
            }
        }

        /// <summary>
        ///     Request each needed privilege.
        /// </summary>
        private void RequestPrivileges()
        {
            foreach (var priv in _privilegesNeeded)
            {
                var result = MLPrivileges.RequestPrivilegeAsync(priv, HandlePrivilegeAsyncRequest);
                if (!result.IsOk)
                {
                    Debug.LogErrorFormat("{0} Privilege Request Error: {1}", priv, result);
                    _currentPrivilegeState = PrivilegeState.Denied;
                    return;
                }
            }

            _currentPrivilegeState = PrivilegeState.Requested;
        }

        /// <summary>
        ///     Handles the result that is received from the query to the Privilege API.
        ///     If one of the required privileges are denied, set the Privilege state to Denied.
        ///     <param name="result">The resulting status of the query</param>
        ///     <param name="privilegeId">The privilege being queried</param>
        /// </summary>
        private void HandlePrivilegeAsyncRequest(MLResult result, MLPrivilegeId privilegeId)
        {
            if (result.Code == MLResultCode.PrivilegeGranted)
            {
                _privilegesGranted.Add(privilegeId);
                Debug.LogFormat("{0} Privilege Granted", privilegeId);
            }
            else
            {
                Debug.LogErrorFormat("{0} Privilege Error: {1}, disabling example.", privilegeId, result);
                _currentPrivilegeState = PrivilegeState.Denied;
            }
        }

        #endregion
    }
}