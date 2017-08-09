using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;


public class KinectFbxRecorder : MonoBehaviour 
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	[Tooltip("Whether the avatar's head rotation is controlled externally (e.g. by VR-headset)." )]
	public bool externalHeadRotation = false;

	[Tooltip("Whether the avatar's feet must stick to the ground.")]
	public bool groundedFeet = false;

	[Tooltip("Whether to apply the humanoid muscle limits to the avatar bones, or not.")]
	public bool applyMuscleLimits = false;

	[Tooltip("Reference to the LeapMotion hand-pool, if you want to track the user hands with LeapMotion sensor.")]
	public Leap.Unity.HandPool leapMotionHandPool = null;

//	[Tooltip("Whether the avatar finger orientations are allowed or not.")]
//	private bool fingerOrientations = false;

	[Tooltip("Smooth factor used for avatar movements and joint rotations.")]
	public float smoothFactor = 10f;

	// path to the file to load
	[Tooltip("Path to the fbx file, used to load the model.")]
	public string loadFilePath;
	
	[Tooltip("Path to the fbx file, used to save the model and animation.")]
	public string saveFilePath = string.Empty;

	[Tooltip("Whether to append timestamp to the save file name. If this setting is enabled, each recording will be saved to a separate fbx-file.")]
	public bool timestampSaveFileName = false;

	[Tooltip("Whether to load the model from save-to path, if the save-file exists.")]
	public bool loadSaveFileIfExists = false;

	[Tooltip("Name of the saved animation.")]
	public string animationName = string.Empty;

	[Tooltip("Unity scale factor of the loaded fbx model.")]
	public float importScaleFactor = 1f;
	
	[Tooltip("Output file format. Leave empty for the default format.")]
	public string outputFileFormat = string.Empty;

	[Tooltip("Maximum FPS (frame rate) for saving animation frames. The time between frames will be minimum 1/FPS seconds.")]
	public float maxFramesPerSecond = 0f;

	[Tooltip("Joint position is saved, if the distance to last position is greater than the threshold (in meters).")]
	public float jointDistanceThreshold = 0.01f;

	[Tooltip("Joint orientation is saved, if the angle to last orientation is greater than the threshold (in degrees).")]
	public float jointAngleThreshold = 0.1f;

	[Tooltip("Whether not to include the root position into the saved animation.")]
	public bool dontSaveRootPos = false;

	[Tooltip("Whether not to include the clavicle orientations into the saved animation.")]
	public bool dontSaveClavicles = false;

	[Tooltip("Whether not to include the finger orientations into the saved animation.")]
	public bool dontSaveFingers = false;

	[Tooltip("Joints that will not be included into saved animation.")]
	public List<KinectInterop.JointType> dontSaveJoints = new List<KinectInterop.JointType>();
	
	[Tooltip("UI-Text to display information messages.")]
	public UnityEngine.UI.Text infoText;
	
	[Tooltip("Whether to start recording, right after the scene starts.")]
	public bool recordAtStart = false;

	[Tooltip("Sound to play when the recording is started.")]
	public AudioSource soundRecStarted;

	[Tooltip("Sound to play when the recording is stopped.")]
	public AudioSource soundRecStopped;


	// the avatar object
	private GameObject avatarObject = null;

	// the recorded model
	private GameObject recordedObject = null;

	// whether it is recording at the moment
	private bool isRecording = false;

	// reference to KinectManager
	private KinectManager manager = null;

	// reference to AvatarController
	private AvatarController avatarCtrl = null;

	// initial avatar position
	private Vector3 initialPos = Vector3.zero;

	// last saved positions and rotations of the avatar joints
	private Vector3[] lastSavedPos;
	private Quaternion[] lastSavedRot;

	// local rotations of the bone transform
	private Quaternion[] alJointPreRots = null;
	private Quaternion[] alJointPostRots = null;

	// time variables used for recording
	private long liRelTime = 0;
	private float fStartTime = 0f;
	private float fCurrentTime = 0f;
	private float fMinFrameTime = 0f;
	private int iSavedFrame = 0;

	// unity scale factor
	private float fUnityScale = 0f;

	// fbx global time mode as FPS
	private float fGlobalFps = 0f;

	// fbx global scale factor
	private float fFbxScale = 0f;

	// if the fbx wrapper is available
	private bool bFbxAvailable = false;

	// if the fbx-system is successfully initialized
	private bool bFbxInited = false;
	
	// if the fbx-file was successfully loaded
	private bool bFbxLoaded = false;
	
	// if the fbx file is modified and needs to be saved
	private bool bFbxDirty = false;


	/// <summary>
	/// Starts the recording.
	/// </summary>
	/// <returns><c>true</c>, if recording was started, <c>false</c> otherwise.</returns>
	public bool StartRecording()
	{
		if(avatarObject == null)
			return false;

		if(isRecording)
			return false;

		isRecording = true;

		// load the fbx file
		if(!LoadInputFile())
		{
			isRecording = false;

			Debug.LogError("File could not be loaded: " + loadFilePath);
			if(infoText != null)
			{
				infoText.text = "File could not be loaded: " + loadFilePath;
			}
		}

		// check for save file name
		if(saveFilePath == string.Empty)
		{
			isRecording = false;

			Debug.LogError("No file to save.");
			if(infoText != null)
			{
				infoText.text = "No file to save.";
			}
		}
		
		// create the animation stack, if animation name is specified
		if(animationName != string.Empty && !MocapFbxWrapper.CreateAnimStack(animationName))
		{
			isRecording = false;

			//throw new Exception("Could not create animation: " + animationName);
			Debug.LogError("Could not create animation: " + animationName);
			if(infoText != null)
			{
				infoText.text = "Could not create animation: " + animationName;
			}
		}
		
		if(isRecording && animationName != string.Empty)
		{
			MocapFbxWrapper.SetCurrentAnimStack(animationName);
		}
		
		if(isRecording)
		{
			Debug.Log("Recording started.");
			if(infoText != null)
			{
				infoText.text = "Recording... Say 'Stop' to stop the recorder.";
			}

			if (soundRecStarted && !soundRecStarted.isPlaying) 
			{
				soundRecStarted.Play();
			}

			// get initial avatar position
			if(avatarCtrl != null)
			{
				int hipsIndex = avatarCtrl.GetBoneIndexByJoint(KinectInterop.JointType.SpineBase, false);
				Transform hipsTransform = avatarCtrl.GetBoneTransform(hipsIndex);
				initialPos = hipsTransform != null ? hipsTransform.position : Vector3.zero;
			}

			// initialize times
			fStartTime = fCurrentTime = Time.time;
			fMinFrameTime = maxFramesPerSecond > 0f ? 1f / maxFramesPerSecond : 0f;
			iSavedFrame = 0;

			// get global time mode as fps
			fGlobalFps = MocapFbxWrapper.GetGlobalFps();

			// get global scale factor
			fFbxScale = 100f / MocapFbxWrapper.GetGlobalScaleFactor();

			// Unity scale factor
			fUnityScale = importScaleFactor != 0f ? 1f / importScaleFactor : 1f;
		}

		return isRecording;
	}


	/// <summary>
	/// Stops the recording.
	/// </summary>
	public void StopRecording()
	{
		if(isRecording)
		{
			if (soundRecStopped && !soundRecStopped.isPlaying) 
			{
				soundRecStopped.Play();
			}

			SaveOutputFileIfNeeded();
			isRecording = false;

			Debug.Log("Recording stopped.");
			if(infoText != null)
			{
				infoText.text = "Recording stopped.";
			}
		}

		if(infoText != null)
		{
			infoText.text = "Say: 'Record' to start the recorder.";
		}
	}


	/// <summary>
	/// Determines whether file recording is in progress at the moment
	/// </summary>
	/// <returns><c>true</c> if file recording is in progress; otherwise, <c>false</c>.</returns>
	public bool IsRecording()
	{
		return isRecording;
	}


	/// <summary>
	/// Gets the avatar controller.
	/// </summary>
	/// <returns>The avatar controller.</returns>
	public AvatarController GetAvatarController()
	{
		return avatarCtrl;
	}


	// ----- end of public functions -----
	

	// loads the specified fbx file
	private bool LoadInputFile()
	{
		if(!bFbxInited)
			return false;
		
		if(!SaveOutputFileIfNeeded())
			throw new Exception("Could not save the modified file to: " + saveFilePath);

		string sLoadFbxFile = loadFilePath;
		if (loadSaveFileIfExists && saveFilePath != string.Empty && File.Exists (saveFilePath)) 
		{
			sLoadFbxFile = saveFilePath;
		}
		
		if(sLoadFbxFile != string.Empty)
			bFbxLoaded = MocapFbxWrapper.LoadFbxFile(sLoadFbxFile, false);
		else
			bFbxLoaded = false;
		
		if (avatarCtrl && manager /**&& manager.IsInitialized()*/) 
		{
			// get the pre- and post-rotations
			int jointCount = manager.GetJointCount();
			int fingerCount = avatarCtrl.GetFingerTransformCount();

			lastSavedPos = new Vector3[jointCount + fingerCount + 2];
			lastSavedRot = new Quaternion[jointCount + fingerCount + 2];

			for (int j = 0; j < (jointCount + fingerCount + 2); j++) 
			{
				lastSavedPos[j] = Vector3.zero;
				lastSavedRot[j] = Quaternion.identity;
			}

			alJointPreRots = new Quaternion[jointCount + fingerCount + 2];
			alJointPostRots = new Quaternion[jointCount + fingerCount + 2];

			for (int j = 0; j < jointCount; j++) 
			{
				int boneIndex = avatarCtrl.GetBoneIndexByJoint ((KinectInterop.JointType)j, false);
				Transform jointTransform = avatarCtrl.GetBoneTransform (boneIndex);

				alJointPreRots[j] = GetJointPreRot(jointTransform);
				alJointPostRots[j] = GetJointPostRot(jointTransform);
			}

			for (int f = 0; f < fingerCount; f++) 
			{
				Transform fingerTransform = avatarCtrl.GetFingerTransform(f);

				alJointPreRots[jointCount + f] = GetJointPreRot(fingerTransform);
				alJointPostRots[jointCount + f] = GetJointPostRot(fingerTransform);
			}

			// get special transforms
			int specIndex = avatarCtrl.GetSpecIndexByJoint (KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.SpineShoulder, false);
			Transform specTransform = avatarCtrl.GetBoneTransform (specIndex);
			alJointPreRots[jointCount + fingerCount] = GetJointPreRot (specTransform);
			alJointPostRots[jointCount + fingerCount] = GetJointPostRot (specTransform);

			specIndex = avatarCtrl.GetSpecIndexByJoint (KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.SpineShoulder, false);
			specTransform = avatarCtrl.GetBoneTransform (specIndex);
			alJointPreRots[jointCount + fingerCount + 1] = GetJointPreRot (specTransform);
			alJointPostRots[jointCount + fingerCount + 1] = GetJointPostRot (specTransform);
		} 
		else 
		{
			bFbxLoaded = false;
		}

		bFbxDirty = false;

		return bFbxLoaded;
	}


	// saves the specified fbx file
	private bool SaveOutputFile()
	{
		if(saveFilePath == string.Empty)
		{
			saveFilePath = loadFilePath;
		}

		string _saveFilePath = timestampSaveFileName ? AddTimestampToSaveFileName(saveFilePath) : saveFilePath;

		// recorded animator controller
		int iP = _saveFilePath.LastIndexOf('/');
		string animatorFileName = (iP >= 0 ? _saveFilePath.Substring(0, iP) : "Assets") + "/RecordedAnim.controller";
		string animatorMetaName = (iP >= 0 ? _saveFilePath.Substring(0, iP) : "Assets") + "/RecordedAnim.controller.meta";

		// delete the old csv file
		if(_saveFilePath != string.Empty && File.Exists(_saveFilePath))
		{
			File.Delete(_saveFilePath);

			string saveMetaFilePath = _saveFilePath + ".meta";
			if (File.Exists(saveMetaFilePath)) 
			{
				File.Delete(saveMetaFilePath);
			}

			if (File.Exists(animatorFileName)) 
			{
				File.Delete(animatorFileName);
			}

			if (File.Exists(animatorMetaName)) 
			{
				File.Delete(animatorMetaName);
			}

#if UNITY_EDITOR
			UnityEditor.AssetDatabase.Refresh();
#endif
		}
		
		if(_saveFilePath != string.Empty)
		{
			if (MocapFbxWrapper.SaveFbxFile(_saveFilePath, outputFileFormat, true, true, false)) 
			{
				// copy the meta file path, too
				string loadMetaFilePath = loadFilePath + ".meta";
				string saveMetaFilePath = _saveFilePath + ".meta";

				if (File.Exists(loadMetaFilePath)) 
				{
					File.Copy(loadMetaFilePath, saveMetaFilePath, true);
				}

#if UNITY_EDITOR
				UnityEditor.AssetDatabase.Refresh(); //.ImportAsset(saveFilePath);

				// create animator controller with single clip
				AnimationClip animationClip = GetRecordedAnimationClip(_saveFilePath);
				if (animationClip)
					UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(animatorFileName, animationClip);
				else
					UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(animatorFileName);

				UnityEditor.AssetDatabase.Refresh (); //.ImportAsset(saveFilePath);
#endif

				bFbxDirty = false;

				// play the animation
//				AnimationPlayer animPlayer = AnimationPlayer.Instance;
//				if (animPlayer) 
//				{
//					animPlayer.PlayAnimationClip(_saveFilePath, animationName);
//				} 
//				else
				{
					// load the recorded model
					InstatiateRecordedModel(_saveFilePath);
				}

				return true;
			}
			else 
			{
				Debug.LogError("Could not save mo-cap to: " + _saveFilePath);
			}
		}
		
		return false;
	}


	// saves the fbx file, if it was modified
	private bool SaveOutputFileIfNeeded()
	{
		if(bFbxDirty)
		{
			return SaveOutputFile();
		}
		
		return true;
	}

	// adds time stamp to the save file name
	private string AddTimestampToSaveFileName(string saveFilePath)
	{
		if (saveFilePath == string.Empty)
			return string.Empty;

		int iP = saveFilePath.LastIndexOf('.');
		string _saveFilePath = iP >= 0 ? saveFilePath.Substring(0, iP) : saveFilePath;

		_saveFilePath += "-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".fbx";

		return _saveFilePath;
	}


	// returns the saved animation clip
	private AnimationClip GetRecordedAnimationClip(string saveFilePath)
	{
		if (saveFilePath == string.Empty)
			return null;

#if UNITY_EDITOR
		// get the objects in the saved file
		UnityEngine.Object[] fbxObjects = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(saveFilePath);

		if(fbxObjects != null)
		{
			foreach(UnityEngine.Object fbxObject in fbxObjects)
			{
				if(fbxObject is AnimationClip && fbxObject.name == animationName)
				{
					AnimationClip fbxAnimClip = (AnimationClip)fbxObject;
					return fbxAnimClip;
				}
			}
		}
#endif

		return null;
	}

	// loads the avatar object from the input fbx file
	private bool InstatiateLiveReplay()
	{
		if(loadFilePath == string.Empty)
			return false;

#if UNITY_EDITOR
		UnityEngine.Object avatarModel = UnityEditor.AssetDatabase.LoadAssetAtPath(loadFilePath, typeof(UnityEngine.Object));
		if(avatarModel == null)
			return false;

		if(avatarObject != null) 
		{
			GameObject.Destroy(avatarObject);
		}
		
		avatarObject = (GameObject)GameObject.Instantiate(avatarModel);
		if(avatarObject != null)
		{
			avatarObject.transform.position = new Vector3(-1f, 0f, -1f); // Vector3.zero;
			avatarObject.transform.rotation =  Quaternion.identity;

			AvatarController ac = avatarObject.GetComponent<AvatarController>();
			if(ac == null)
			{
				ac = avatarObject.AddComponent<AvatarController>();
				ac.playerIndex = playerIndex;

				ac.mirroredMovement = false;
				ac.verticalMovement = true;

				ac.smoothFactor = smoothFactor;
				//ac.fingerOrientations = fingerOrientations;
				ac.applyMuscleLimits = applyMuscleLimits;
				ac.groundedFeet = groundedFeet;
				ac.externalHeadRotation = externalHeadRotation;

				//ac.Awake();
			}

			KinectManager km = KinectManager.Instance;

			if(km)
			{
				if(km.IsUserDetected())
				{
					ac.SuccessfulCalibration(km.GetPrimaryUserID(), false);
				}
				
				km.avatarControllers.Clear();
				km.avatarControllers.Add(ac);
			}

			if(leapMotionHandPool != null && leapMotionHandPool.gameObject.activeInHierarchy)
			{
				// setup left hand
				int boneIndex = ac.GetBoneIndexByJoint (KinectInterop.JointType.WristLeft, false);
				Transform transLeftHand = ac.GetBoneTransform(boneIndex);

				Leap.Unity.LeapRiggedHand riggedLHand = transLeftHand.gameObject.GetComponent<Leap.Unity.LeapRiggedHand>();
				if(riggedLHand == null)
				{
					riggedLHand = transLeftHand.gameObject.AddComponent<Leap.Unity.LeapRiggedHand>();
					riggedLHand.Handedness = Leap.Unity.Chirality.Left;
					riggedLHand.smoothFactor = smoothFactor;
					riggedLHand.SetupRiggedHand();
				}

				// fix left thumb's finger-pointing vector
				Leap.Unity.LeapRiggedFinger riggedLThumb = (Leap.Unity.LeapRiggedFinger)riggedLHand.fingers[(int)Leap.Finger.FingerType.TYPE_THUMB];
				if(riggedLThumb != null)
				{
					riggedLThumb.modelFingerPointing = new Vector3(-1f, 0f, 1f);
				}

				// setup right hand
				boneIndex = ac.GetBoneIndexByJoint (KinectInterop.JointType.WristRight, false);
				Transform transRightHand = ac.GetBoneTransform(boneIndex);

				Leap.Unity.LeapRiggedHand riggedRHand = transRightHand.gameObject.GetComponent<Leap.Unity.LeapRiggedHand>();
				if(riggedRHand == null)
				{
					riggedRHand = transRightHand.gameObject.AddComponent<Leap.Unity.LeapRiggedHand>();
					riggedRHand.Handedness = Leap.Unity.Chirality.Right;
					riggedRHand.smoothFactor = smoothFactor;
					riggedRHand.SetupRiggedHand();
				}

				// fix right thumb's finger-pointing vector
				Leap.Unity.LeapRiggedFinger riggedRThumb = (Leap.Unity.LeapRiggedFinger)riggedRHand.fingers[(int)Leap.Finger.FingerType.TYPE_THUMB];
				if(riggedRThumb != null)
				{
					riggedRThumb.modelFingerPointing = new Vector3(1f, 0f, 1f);
				}

				// add hands to the pool
				leapMotionHandPool.AddNewGroup("LeapRiggedHands", riggedLHand, riggedRHand);
				leapMotionHandPool.Start();  // start the pool to fill in the models

				// set non-kinect hand control for avatar's hands
				ac.externalHandRotations = true;
			}
			else
			{
				// use Kinect hand controls
				ac.externalHandRotations = false;
				ac.fingerOrientations = true;
			}
		}

		return true;
#else
		return false;
#endif
	}


	// loads the recorded model from the output fbx file
	private bool InstatiateRecordedModel(string saveFilePath)
	{
		if(saveFilePath == string.Empty)
			return false;

#if UNITY_EDITOR
		UnityEngine.Object recordedModel = UnityEditor.AssetDatabase.LoadAssetAtPath(saveFilePath, typeof(UnityEngine.Object));
		if(recordedModel == null)
			return false;

		if(recordedObject != null) 
		{
			GameObject.Destroy(recordedObject);
		}

		// instantiate the recorded model
		recordedObject = (GameObject)GameObject.Instantiate(recordedModel);
		if(recordedObject != null)
		{
			recordedObject.transform.position = new Vector3(1f, 0f, -1f); // Vector3.zero;
			recordedObject.transform.rotation =  Quaternion.identity;

			// recorded animator controller
			int iP = saveFilePath.LastIndexOf('/');
			string animatorFileName = (iP >= 0 ? saveFilePath.Substring(0, iP) : "Assets") + "/RecordedAnim.controller";

			if(File.Exists(animatorFileName))
			{
				UnityEditor.Animations.AnimatorController animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath(animatorFileName, typeof(UnityEngine.Object)) 
					as UnityEditor.Animations.AnimatorController;

				Animator animator = recordedObject.GetComponent<Animator>();
				animator.applyRootMotion = false;

				if(animator && animatorController)
				{
					animator.runtimeAnimatorController = animatorController;
				}
			}

			AnimationPlayer animPlayer = recordedObject.AddComponent<AnimationPlayer>();
			animPlayer.PlayAnimationClip(saveFilePath, animationName);
		}

		return true;
#else
		return false;
#endif
	}


	// returns pre-rotation of a joint (fbx node)
	private Quaternion GetJointPreRot(Transform jointTransform)
	{
		if(jointTransform != null)
		{
			string sJointName = jointTransform.name;
			Vector3 vPreRot = Vector3.zero;
			MocapFbxWrapper.GetNodePreRot(sJointName, ref vPreRot);
			
			Quaternion qPreRot = Quaternion.identity;
			MocapFbxWrapper.Rot2Quat(ref vPreRot, ref qPreRot);
			
			qPreRot.y = -qPreRot.y;
			qPreRot.z = -qPreRot.z;
			
			return qPreRot;
		}

		return Quaternion.identity;
	}


	// returns post-rotation of a joint (fbx node)
	private Quaternion GetJointPostRot(Transform jointTransform)
	{
		if(jointTransform != null)
		{
			string sJointName = jointTransform.name;
			Vector3 vPostRot = Vector3.zero;
			MocapFbxWrapper.GetNodePostRot(sJointName, ref vPostRot);
			
			Quaternion qPostRot = Quaternion.identity;
			MocapFbxWrapper.Rot2Quat(ref vPostRot, ref qPostRot);
			
			qPostRot.y = -qPostRot.y;
			qPostRot.z = -qPostRot.z;
			
			return qPostRot;
		}
		
		return Quaternion.identity;
	}
	
	
	// saves needed animation frames at the current save-time
	private bool SaveAnimFrame(int jointIndex, Transform jointTransform, StringBuilder sbDebug)
	{
		if(jointTransform == null)
			return false;
		
		string sJointName = jointTransform.name;
		bool bSuccess = true;
		
		float fSaveTime = (float)iSavedFrame / fGlobalFps;
		
		if(jointIndex == 0 && !dontSaveRootPos)
		{
			// save avatar position
			Vector3 currentPos = jointTransform.position - initialPos;
			currentPos.x = -currentPos.x;
			currentPos.y += initialPos.y;

			float distToLast = (currentPos - lastSavedPos[jointIndex]).magnitude;
			if (distToLast >= jointDistanceThreshold) 
			{
				lastSavedPos[jointIndex] = currentPos;

				if (sbDebug != null) 
				{
					sbDebug.AppendFormat ("{0}:{1:F2} ", jointIndex, distToLast);
				}

				Vector3 vJointPos = currentPos * fUnityScale * fFbxScale;
				bSuccess &= MocapFbxWrapper.SetAnimCurveTrans(sJointName, fSaveTime, ref vJointPos);
			}
		}

		// save joint orientation
		Quaternion qJointRot = jointTransform.localRotation;
		float angleToLast = Quaternion.Angle(lastSavedRot[jointIndex], qJointRot);

		if (angleToLast >= jointAngleThreshold) 
		{
			lastSavedRot[jointIndex] = qJointRot;

			if (sbDebug != null) 
			{
				sbDebug.AppendFormat ("{0}:{1:F0} ", jointIndex, angleToLast);
			}

			if(alJointPreRots != null && alJointPostRots != null)
			{
				Quaternion qJointPreRot =  alJointPreRots[jointIndex];
				Quaternion qJointPostRot = alJointPostRots[jointIndex];

				qJointRot = Quaternion.Inverse(qJointPreRot) * qJointRot * Quaternion.Inverse(qJointPostRot);
			}

			qJointRot.x = -qJointRot.x;
			qJointRot.w = -qJointRot.w;

			Vector3 vJointRot = Vector3.zero;
			MocapFbxWrapper.Quat2Rot(ref qJointRot, ref vJointRot);

			bSuccess &= MocapFbxWrapper.SetAnimCurveRot(sJointName, fSaveTime, ref vJointRot);
		}
		
		if(iSavedFrame == 0)
		{
			// save the node scale in the first frame only
			Vector3 vJointScale = jointTransform.localScale;
			MocapFbxWrapper.SetAnimCurveScale(sJointName, 0f, ref vJointScale);
		}
		
		return bSuccess;
	}


	void Awake()
	{
		try 
		{
			// ensure the fbx wrapper is available
			bool bNeedRestart = false;
			if(MocapFbxWrapper.EnsureFbxWrapperAvailability(ref bNeedRestart))
			{
				bFbxAvailable = true;
				
				if(bNeedRestart)
				{
					//KinectInterop.RestartLevel(gameObject, "MF");
					return;
				}
			}
			else
			{
				throw new Exception("Fbx-Unity-Wrapper is not available!");
			}
		} 
		catch (Exception ex) 
		{
			Debug.LogError(ex.Message);
			Debug.LogException(ex);
			
			if(infoText != null)
			{
				infoText.text = ex.Message;
			}
		}
	}
	

	void Start()
	{
		// check if fbx-wrapper is available
		if(!bFbxAvailable)
			return;

		try 
		{
			// instantiate the avatar object from the fbx file
			if(InstatiateLiveReplay() && avatarObject)
			{
				avatarCtrl = avatarObject.gameObject.GetComponent<AvatarController>();
			}
			else
			{
				if(loadFilePath == string.Empty)
					throw new Exception("Please specify fbx-file to load.");
				else
					throw new Exception("Cannot instantiate avatar from file: " + loadFilePath);
			}

			// load the recorded model, if it exists
			InstatiateRecordedModel(saveFilePath);

			// check the KinectManager availability
			if(!manager)
			{
				manager = KinectManager.Instance;
			}

			if(!manager)
			{
				throw new Exception("KinectManager not found, probably not initialized. See the log for details");
			}

			// initialize fbx wrapper
			if(!bFbxInited)
			{
				bFbxInited = MocapFbxWrapper.InitFbxWrapper();
				
				if(!bFbxInited)
				{
					throw new Exception("Fbx wrapper could not be initialized.");
				}
			}
			
			if(infoText != null)
			{
				infoText.text = "Say: 'Record' to start the recorder.";
			}

//			// play the animation
//			AnimationPlayer animPlayer = AnimationPlayer.Instance;
//			if (animPlayer) 
//			{
//				animPlayer.PlayAnimationClip (saveFilePath, animationName);
//			}

			if(recordAtStart)
			{
				StartRecording();
			}
		} 
		catch (Exception ex) 
		{
			Debug.LogError(ex.Message);
			Debug.LogException(ex);
			
			if(infoText != null)
			{
				infoText.text = ex.Message;
			}
		}
	}


	void Update () 
	{
		if(isRecording && animationName != string.Empty)
		{
			// save the body frame, if any
			if(manager /**&& manager.IsInitialized()*/)
			{
				long userId = manager.GetUserIdByIndex(playerIndex);
				long liFrameTime = manager.GetBodyFrameTimestamp();
				//long liFrameTime = (long)(Time.time * 1000);

				if(userId != 0 && avatarCtrl && liFrameTime != liRelTime &&
					Time.time >= (fCurrentTime + fMinFrameTime))
				{
					//Debug.Log("Saving @ time: " + Time.time + ", delta: " + (Time.time - fCurrentTime));

					liRelTime = liFrameTime;
					fCurrentTime = Time.time;

					float fRelTime = fCurrentTime - fStartTime;
					int iCurrentFrame = Mathf.FloorToInt(fRelTime * fGlobalFps);

					if(infoText)
					{
						infoText.text = string.Format("Recording @ {0:F3}... Say 'Stop' to stop the recorder.", fRelTime);
					}
					
					int jointCount = manager.GetJointCount();
					int fingerCount = avatarCtrl.GetFingerTransformCount();
					//bool bSavedOnce = false;

					StringBuilder sbBuf = new StringBuilder();
					const char delimiter = ',';

					//Quaternion qRootRotation = Quaternion.identity;
					//int iSavedFrame1 = iSavedFrame;

					while(iSavedFrame <= iCurrentFrame)
					{
						StringBuilder sbDebug = null; // new StringBuilder();

						if (sbDebug != null) 
						{
							sbDebug.AppendFormat ("{0} - ", iSavedFrame);
						}

						// save properties of all joints
						for(int j = 0; j < jointCount; j++)
						{
							if(dontSaveJoints.Count > 0 && dontSaveJoints.Contains((KinectInterop.JointType)j))
								continue;
							
							int boneIndex = avatarCtrl.GetBoneIndexByJoint((KinectInterop.JointType)j, false);
							Transform jointTransform = avatarCtrl.GetBoneTransform(boneIndex);
							
							if(jointTransform != null)
							{
								bool bSuccess = SaveAnimFrame(j, jointTransform, sbDebug);

								if(!bSuccess)
								{
									string sMessage = string.Format("Could not save animation frame for joint '{0}' at time: {1:F3}", (KinectInterop.JointType)j, fRelTime);
									
									infoText.text = sMessage;
									Debug.LogError(sMessage);
								}
								
								bFbxDirty = bSuccess;
							}
						}

						if (!dontSaveFingers /**&& leapMotionHandPool && leapMotionHandPool.gameObject.activeInHierarchy*/) 
						{
							for(int f = 0; f < fingerCount; f++)
							{
								Transform fingerTransform = avatarCtrl.GetFingerTransform(f);

								if(fingerTransform != null)
								{
									bool bSuccess = SaveAnimFrame(jointCount + f, fingerTransform, sbDebug);

									if(!bSuccess)
									{
										string sMessage = string.Format("Could not save animation frame for finger '{0}' at time: {1:F3}", f, fRelTime);

										infoText.text = sMessage;
										Debug.LogError(sMessage);
									}

									bFbxDirty = bSuccess;
								}
							}
						}

						// save properties of the special joints
						if (!dontSaveClavicles) 
						{
							int specIndex = avatarCtrl.GetSpecIndexByJoint(KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.SpineShoulder, false);
							Transform specTransform = avatarCtrl.GetBoneTransform(specIndex);
							bFbxDirty |= SaveAnimFrame(jointCount + fingerCount, specTransform, sbDebug);

							specIndex = avatarCtrl.GetSpecIndexByJoint(KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.SpineShoulder, false);
							specTransform = avatarCtrl.GetBoneTransform(specIndex);
							bFbxDirty |= SaveAnimFrame(jointCount + fingerCount + 1, specTransform, sbDebug);
						}

						if (sbDebug != null)
						{
							Debug.Log(sbDebug.ToString ());
						}
						
						//bSavedOnce = true;
						iSavedFrame++;
					}

					// remove the last delimiter
					if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
					{
						sbBuf.Remove(sbBuf.Length - 1, 1);
					}

				}
			}
		}

	}

	void OnDestroy()
	{
		// check if fbx-wrapper is available
		if(!bFbxAvailable)
			return;

		// finish recording, if needed
		isRecording = false;

		// save the fbx file, if needed
		if(!SaveOutputFileIfNeeded())
		{
			//Debug.LogError("Could not save the modified fbx to: " + saveFilePath);
		}
		
		// terminate the fbx wrapper
		if(bFbxInited)
		{
			MocapFbxWrapper.TermFbxFrapper();
			bFbxInited = false;
		}
	}


}
