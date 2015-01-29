﻿//	============================================================
//	Name:		UMABonePose
//	Author: 	Eli Curtz
//	Copyright:	(c) 2013 Eli Curtz
//	============================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;

using UMA;

namespace PoseTools.UMA {

[System.Serializable]
public class UMABonePose : ScriptableObject {
	
	[System.Serializable]
	public class PoseBone {
		public string bone;
		public int hash;
		
		public Vector3 position;
		public Quaternion rotation;
		public Vector3 scale;
	}
		
	public PoseBone[] poses;
	
	public UMABonePose[] tweenPoses = null;
	public float[] tweenWeights = null;

	/*
    public UMABonePose Duplicate()
    {
        UMABonePose tempPose = CreateInstance<UMABonePose>();

		tempPose.poses = new UMABonePose.PoseBone[poses.Length];

        for (int i = 0; i < poses.Length; i++)
        {
            tempPose.poses[i].bone = poses[i].bone;
            tempPose.poses[i].position = poses[i].position;
            tempPose.poses[i].rotation = poses[i].rotation;
            tempPose.poses[i].scale = poses[i].scale;
        }

        return tempPose;
    }
    */

	void Reset() {
		poses = new PoseBone[0];
	}
	
	void OnEnable() {
		if (poses == null) {
			poses = new PoseBone[0];
		}

		foreach (PoseBone pose in poses) {
			if (pose.hash == 0) {
				pose.hash = UMASkeleton.StringToHash(pose.bone);
			}
		}
	}
	
	public int PoseCount() {
		if (poses != null) {
			return poses.Length;
		}
		
		return 0;
	}
	
#if UNITY_EDITOR
	public void AddBone(Transform bone, Vector3 position, Quaternion rotation, Vector3 scale) {
		PoseBone pose = new PoseBone();
		pose.bone = bone.name;
		pose.hash = UMASkeleton.StringToHash(bone.name);
		pose.position = position - bone.localPosition;
		pose.rotation = Quaternion.Inverse(bone.localRotation) * rotation;
		pose.scale = new Vector3(scale.x / bone.localScale.x,
								scale.y / bone.localScale.y,
								scale.z / bone.localScale.z);
		
		ArrayUtility.Add(ref poses, pose);
	}
	
	/*
	public void CalculatePoses() {
		if (!sourceSkeleton || !poseSkeleton) {
			Debug.LogError("Missing transform.");
			return;
		}
		
		List<PoseBone> poseList = new List<PoseBone>();
		Transform[] sourceBones = GetTransformsInPrefab(sourceSkeleton);
		Transform[] poseBones = GetTransformsInPrefab(poseSkeleton);
		
		foreach (Transform bone in poseBones) {
			Transform source = System.Array.Find<Transform>(sourceBones, entry => entry.name == bone.name);
			if (source) {
				if ((bone.localPosition != source.localPosition) ||
					(bone.localRotation != source.localRotation) ||
					(bone.localScale != source.localScale))
				{
					PoseBone pose = new PoseBone();
					pose.bone = bone.name;
					pose.position = bone.localPosition - source.localPosition;
					pose.rotation = bone.localRotation * Quaternion.Inverse(source.localRotation);
					pose.scale = new Vector3(bone.localScale.x / source.localScale.x,
											bone.localScale.y / source.localScale.y,
											bone.localScale.z / source.localScale.z);
					
					poseList.Add(pose);
				}
			}
			else {
				Debug.Log("Unmatched bone: "+bone.name);
			}
		}
		
		poses = poseList.ToArray();
	}
	*/
#endif
	
	protected float ApplyPoseTweens(UMASkeleton umaSkeleton, float weight) {
		int tweenCount = tweenPoses.Length;
		if (tweenWeights.Length != tweenCount) {
			Debug.LogError("Tween pose / weight mismatch!");
			return weight;
		}
		
		// weight <= first tween weight
		if (weight <= tweenWeights[0]) {
			weight = weight / tweenWeights[0];
			tweenPoses[0].ApplyPose(umaSkeleton, weight);
			return 0f;
		}
		// weight >= last tween weight
		else if (weight >= tweenWeights[tweenCount - 1]) {
			float weightRange = 1f - tweenWeights[tweenCount - 1];
			float lowerWeight = (1f - weight) / weightRange;
			tweenPoses[tweenCount - 1].ApplyPose(umaSkeleton, lowerWeight);
			return (1f - lowerWeight);
		}
		// first tween weight < weight < last tween weight
		else {
			int tween = 1;
			while (weight > tweenWeights[tween]) {
				tween++;
			}
			
			float lowerWeight = tweenWeights[tween - 1];
			float upperWeight = tweenWeights[tween];
			float weightRange = upperWeight - lowerWeight;
			lowerWeight = (upperWeight - weight) / weightRange;
			tweenPoses[tween - 1].ApplyPose(umaSkeleton, lowerWeight);
			upperWeight = 1f - lowerWeight;
			tweenPoses[tween].ApplyPose(umaSkeleton, upperWeight);
			return 0f;
		}
	}

	public void ApplyPose(UMASkeleton umaSkeleton, float weight) {
		if ((poses == null) || (umaSkeleton == null)) {
			Debug.LogError("Missing poses or skeleton!");
			return;
		}
		
		if ((tweenPoses != null) && (tweenPoses.Length > 0) && (weight < 1f)) {
			weight = ApplyPoseTweens(umaSkeleton, weight);
		}

		if (weight <= 0f) {
			return;
		}

		foreach (PoseBone pose in poses) {
			if (umaSkeleton.HasBone(pose.hash)) {
				Vector3 position = umaSkeleton.GetPosition(pose.hash);
				Quaternion rotation = umaSkeleton.GetRotation(pose.hash);
				Vector3 scale = umaSkeleton.GetScale(pose.hash);
				position += pose.position * weight;
				Quaternion posedRotation = rotation * pose.rotation;
				rotation = Quaternion.Slerp(rotation, posedRotation, weight);
				scale = Vector3.Lerp(scale, pose.scale, weight);
				umaSkeleton.Set(pose.hash, position, scale, rotation);
			}
			else {
				Debug.Log("Unmatched bone: "+pose.bone);
			}
		}
	}

	/*
	public void ApplyPosesPerBone(Dictionary<string, UMAData.BoneData> boneList, Dictionary<string, float> strengthList) {

		foreach (PoseBone pose in poses) {
			UMAData.BoneData targetBone;
			float strength;
			if (boneList.TryGetValue(pose.bone, out targetBone) && (strengthList.TryGetValue(pose.bone, out strength))) {
				targetBone.boneTransform.localPosition += pose.position * strength;
				Quaternion posedRotation = targetBone.boneTransform.localRotation * pose.rotation;
				targetBone.boneTransform.localRotation = Quaternion.Slerp(targetBone.boneTransform.localRotation, posedRotation, strength);
				targetBone.boneTransform.localScale = Vector3.Slerp(targetBone.boneTransform.localScale, pose.scale, strength);
			}
			else {
				Debug.Log("Unmatched bone: "+pose.bone);
			}
		}
	}
	*/
		
	static private void RecurseTransformsInPrefab(Transform root, List<Transform> transforms)
	{
		for (int i = 0; i < root.childCount; i++) {
			Transform child = root.GetChild(i);
			transforms.Add(child);
			RecurseTransformsInPrefab(child, transforms);
		}
	}
	
	static public Transform[] GetTransformsInPrefab(Transform prefab)
	{
		List<Transform> transforms = new List<Transform>();
		
		RecurseTransformsInPrefab(prefab, transforms);
		
		return transforms.ToArray();
	}
}

}
