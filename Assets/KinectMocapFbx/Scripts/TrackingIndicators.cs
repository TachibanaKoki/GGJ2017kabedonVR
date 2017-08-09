using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrackingIndicators : MonoBehaviour 
{

	public Image leftHandTracker;

	public Image rightHandTracker;

	public RawImage colorCameraImage;

	public Color trackedJointColor = Color.green;

	public Color inferredJointColor = Color.yellow;

	public Color notTrackedJointColor = Color.red;

	// references to managers
	private KinectManager kinectManager;
	private KinectFbxRecorder fbxRecorder;
	private bool isLeapAvailable = false;

	// references to hands
	private Leap.Unity.LeapRiggedHand leftLeapHand;
	private Leap.Unity.LeapRiggedHand rightLeapHand;


	void Start () 
	{
		// get the needed references
		kinectManager = GetComponent<KinectManager>();
		fbxRecorder = GetComponent<KinectFbxRecorder>();

		// check if leap is available
		isLeapAvailable = fbxRecorder && fbxRecorder.leapMotionHandPool && fbxRecorder.leapMotionHandPool.gameObject.activeInHierarchy;
	}
	
	void Update () 
	{
		// show color camera image, as needed
		if (colorCameraImage && !colorCameraImage.texture) 
		{
			if (kinectManager && kinectManager.computeColorMap && kinectManager.IsInitialized()) 
			{
				colorCameraImage.texture = kinectManager.GetUsersClrTex();

				if (colorCameraImage.texture != null) 
				{
					AspectRatioFitter aspectRatioFitter = colorCameraImage.gameObject.GetComponent<AspectRatioFitter>();

					if (aspectRatioFitter != null) 
					{
						aspectRatioFitter.aspectRatio = (float)kinectManager.GetColorImageWidth () / (float)kinectManager.GetColorImageHeight ();
					}
				}
			}
		}

		// get reference to the avatar controller
		AvatarController avatarCtrl = fbxRecorder ? fbxRecorder.GetAvatarController () : null;

		// left hand tracker
		if (avatarCtrl && leftHandTracker) 
		{
			KinectInterop.TrackingState handTracking = KinectInterop.TrackingState.NotTracked;
			//bool bLeapHandExists = leftLeapHand ? leftLeapHand.GetLeapHand() != null : true; // true, in order to get reference to leftLeapHand

			if (isLeapAvailable /**&& bLeapHandExists*/)
			{
				if (!leftLeapHand)
				{
					int handIndex = avatarCtrl.GetBoneIndexByJoint(KinectInterop.JointType.WristLeft, false);
					Transform handTransform = avatarCtrl.GetBoneTransform(handIndex);
					leftLeapHand = handTransform ? handTransform.gameObject.GetComponent<Leap.Unity.LeapRiggedHand>() : null;
				}

				if (leftLeapHand && leftLeapHand.IsHandTracked()) 
				{
					handTracking = KinectInterop.TrackingState.Tracked;
				}

				//Debug.Log ("LeftLeapHand: " + handTracking);
			}

			else if(/**handTracking == KinectInterop.TrackingState.NotTracked &&*/ kinectManager && kinectManager.IsInitialized())
			{
				handTracking = kinectManager.GetJointTrackingState(avatarCtrl.playerId, (int)KinectInterop.JointType.HandLeft);
				//Debug.Log ("LeftKinectHand: " + handTracking);
			}

			leftHandTracker.color = GetTrackingStateColor(handTracking);
		}

		// right hand tracker
		if (avatarCtrl && rightHandTracker) 
		{
			KinectInterop.TrackingState handTracking = KinectInterop.TrackingState.NotTracked;
			//bool bLeapHandExists = rightLeapHand ? rightLeapHand.GetLeapHand() != null : true;  // true, in order to get reference to rightLeapHand
		
			if (isLeapAvailable /**&& bLeapHandExists*/)
			{
				if (!rightLeapHand) 
				{
					int handIndex = avatarCtrl.GetBoneIndexByJoint(KinectInterop.JointType.WristRight, false);
					Transform handTransform = avatarCtrl.GetBoneTransform(handIndex);
					rightLeapHand = handTransform ? handTransform.gameObject.GetComponent<Leap.Unity.LeapRiggedHand>() : null;
				}

				if (rightLeapHand && rightLeapHand.IsHandTracked()) 
				{
					handTracking = KinectInterop.TrackingState.Tracked;
				}

				//Debug.Log ("RightLeapHand: " + handTracking);
			}

			else if(/**handTracking == KinectInterop.TrackingState.NotTracked &&*/ kinectManager && kinectManager.IsInitialized())
			{
				handTracking = kinectManager.GetJointTrackingState(avatarCtrl.playerId, (int)KinectInterop.JointType.HandRight);
				//Debug.Log ("RightKinectHand: " + handTracking);
			}

			rightHandTracker.color = GetTrackingStateColor(handTracking);
		}

	}

	// returns the respective tracking state color
	private Color GetTrackingStateColor(KinectInterop.TrackingState trackingState)
	{
		switch (trackingState) 
		{
		case KinectInterop.TrackingState.Tracked:
			return trackedJointColor;

		case KinectInterop.TrackingState.Inferred:
			return inferredJointColor;

		case KinectInterop.TrackingState.NotTracked:
			return notTrackedJointColor;

		default:
			return Color.white;
		}
	}

}
