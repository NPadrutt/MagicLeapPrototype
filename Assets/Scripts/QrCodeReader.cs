using System;
using System.Collections.Generic;
using TMPro;
using ZXing;
using ZXing.QrCode;
using UnityEngine;
using UnityEngine.XR.MagicLeap;

public class QrCodeReader : MonoBehaviour
{
    public TextMeshPro title;

    #region Private Variables
  private bool _isCameraConnected = false;
    private bool _isCapturing = false;

    private enum PrivilegeState
    {
        Off,
        Started,
        Requested,
        Granted,
        Denied
    }

    private PrivilegeState _currentPrivilegeState = PrivilegeState.Off;

    private MLPrivilegeId[] _privilegesNeeded = { MLPrivilegeId.CameraCapture };

    private List<MLPrivilegeId> _privilegesGranted = new List<MLPrivilegeId>();

    private bool _hasStarted = false;
    #endregion

    #region Unity Methods
    /// <summary>
    /// Start Privilege API.
    /// </summary>
    void OnEnable()
    {
        Debug.LogError("startup.");

        MLResult result = MLPrivileges.Start();
        if (result.IsOk)
        {
            _currentPrivilegeState = PrivilegeState.Started;
        } else
        {
            Debug.LogError("Privilege Error: failed to startup");
            enabled = false;
            return;
        }
    }

    /// <summary>
    /// Stop the camera, unregister callbacks, and stop input and privileges APIs.
    /// </summary>
    void OnDisable()
    {
        if (MLInput.IsStarted)
        {
            MLInput.Stop();
        }

        if (_isCameraConnected)
        {
            MLCamera.OnRawImageAvailable -= OnCaptureRawImageComplete;
            _isCapturing = false;
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
    /// Cannot make the assumption that a reality privilege is still granted after
    /// returning from pause. Return the application to the state where it
    /// requests privileges needed and clear out the list of already granted
    /// privileges. Also, disable the camera and unregister callbacks.
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

            if (_isCameraConnected)
            {
                MLCamera.OnRawImageAvailable -= OnCaptureRawImageComplete;
                _isCapturing = false;
                DisableMLCamera();
            }

            _hasStarted = false;
        }
    }

    /// <summary>
    /// Move through the privilege stages before enabling the feature that requires privileges.
    /// </summary>
    void Update()
    {
        /// Privileges have not yet been granted, go through the privilege states.
        if (_currentPrivilegeState != PrivilegeState.Granted)
        {
            UpdatePrivilege();
        }
        /// Privileges have been granted, enable the feature and run any normal updates items.
        /// Done in a seperate if statement so enable can be done in the same frame as the
        /// privilege is granted.
        if (_currentPrivilegeState == PrivilegeState.Granted)
        {
            StartCapture();
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Connects the MLCamera component and instantiates a new instance
    /// if it was never created.
    /// </summary>
    public bool EnableMLCamera()
    {
        MLResult result = MLCamera.Start();
        if (result.IsOk)
        {
            result = MLCamera.Connect();
            _isCameraConnected = result.IsOk;
        }
        return _isCameraConnected;
    }

    /// <summary>
    /// Disconnects the MLCamera if it was ever created or connected.
    /// </summary>
    public void DisableMLCamera()
    {
        MLCamera.Disconnect();
        // Explicitly set to false here as the disconnect was attempted.
        _isCameraConnected = false;
        MLCamera.Stop();
    }

    /// <summary>
    /// Captures a still image using the device's camera and returns
    /// the data path where it is saved.
    /// </summary>
    /// <param name="fileName">The name of the file to be saved to.</param>
    public void TriggerAsyncCapture()
    {
        if (MLCamera.IsStarted && _isCameraConnected)
        {
            MLResult result = MLCamera.CaptureRawImageAsync();
            if (result.IsOk)
            {
                _isCapturing = true;
            }
        }
    }
    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the event of a new image getting captured.
    /// </summary>
    /// <param name="imageData">The raw data of the image.</param>
    private void OnCaptureRawImageComplete(byte[] imageData)
    {
        _isCapturing = false;

        // Initialize to 8x8 texture so there is no discrepency
        // between uninitalized captures and error texture
        Texture2D texture = new Texture2D(8, 8);
        bool status = texture.LoadImage(imageData);

        try
        {
            IBarcodeReader barcodeReader = new BarcodeReader();
            var result = barcodeReader.Decode(texture.GetPixels32(),
                texture.width, texture.height);
            if (result != null)
            {
                title.text = result.Text;
                Debug.Log("DECODED TEXT FROM QR: " + result.Text);
                MLCamera.StopPreview();
            }
        } catch (Exception ex)
        {
            Debug.LogWarning(ex.Message);
        }
    }
    #endregion

    #region Private Functions
    /// <summary>
    /// Handle the privilege states.
    /// </summary>
    private void UpdatePrivilege()
    {
        switch (_currentPrivilegeState)
        {
            /// Privilege API has been started successfully, ready to make requests.
            case PrivilegeState.Started:
                {
                    RequestPrivileges();
                    break;
                }
            /// Privilege requests have been made, wait until all privileges are granted before enabling the feature that requires privileges.
            case PrivilegeState.Requested:
                {
                    foreach (MLPrivilegeId priv in _privilegesNeeded)
                    {
                        if (!_privilegesGranted.Contains(priv))
                        {
                            return;
                        }
                    }
                    _currentPrivilegeState = PrivilegeState.Granted;
                    break;
                }
            /// Privileges have been denied, respond appropriately.
            case PrivilegeState.Denied:
                {
                    enabled = false;
                    break;
                }
        }
    }

    /// <summary>
    /// Once privileges have been granted, enable the camera and callbacks.
    /// </summary>
    private void StartCapture()
    {
        if (!_hasStarted)
        {
            MLResult result = MLInput.Start();
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

            MLCamera.OnRawImageAvailable += OnCaptureRawImageComplete;
            _hasStarted = true;
            TriggerAsyncCapture();
        }
    }

    /// <summary>
    /// Request each needed privilege.
    /// </summary>
    private void RequestPrivileges()
    {
        foreach (MLPrivilegeId priv in _privilegesNeeded)
        {
            MLResult result = MLPrivileges.RequestPrivilegeAsync(priv, HandlePrivilegeAsyncRequest);
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
    /// Handles the result that is received from the query to the Privilege API.
    /// If one of the required privileges are denied, set the Privilege state to Denied.
    /// <param name="result">The resulting status of the query</param>
    /// <param name="privilegeId">The privilege being queried</param>
    /// </summary>
    private void HandlePrivilegeAsyncRequest(MLResult result, MLPrivilegeId privilegeId)
    {
        if (result.Code == MLResultCode.PrivilegeGranted)
        {
            _privilegesGranted.Add(privilegeId);
            Debug.LogFormat("{0} Privilege Granted", privilegeId);
        } else
        {
            Debug.LogErrorFormat("{0} Privilege Error: {1}, disabling example.", privilegeId, result);
            _currentPrivilegeState = PrivilegeState.Denied;
        }
    }
    #endregion
}
