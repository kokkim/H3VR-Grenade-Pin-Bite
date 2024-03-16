using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using FistVR;
using BepInEx;
using BepInEx.Configuration;

namespace GrenadePinBite
{
	[BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
	[BepInProcess("h3vr.exe")]
	public class GrenadePinBite : BaseUnityPlugin
	{
		//TODO: FIX MULTI-PIN GRENADES

		private const string ASSET_BUNDLE_NAME = "grenadepinbite";
		GameObject toothPrefab;
		AudioEvent AudEvent_Spit = new();

		public static ConfigEntry<float> biteRadius;
		public static ConfigEntry<float> forceRequiredForPull;
		public static ConfigEntry<float> pinPullToothChance;

		bool isPinInMouth;	//true if pin is loose inside mouth, already pulled off the grenade
		PinnedGrenadeRing? curBittenRing;	//The currently bitten ring still attached to a grenade
		FVRPhysicalObject? pinInMouth;  //loose pin inside mouth, not attached to a grenade

		Vector3 prevFramePinPos = new();
		float timeUntilSpitOut = 0.5f;

		public GrenadePinBite()
		{
			On.FistVR.PinnedGrenade.UpdateInteraction += PinnedGrenade_UpdateInteraction;
			On.FistVR.GM.InitScene += GM_InitScene;

			//-----------------------------------------------------------

			string pluginPath = Path.GetDirectoryName(Info.Location);
			AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(pluginPath, ASSET_BUNDLE_NAME));
			toothPrefab = bundle.LoadAsset<GameObject>("ToothPrefab");

			//Spit audio
			for (int i = 1; i <= 6; i++)
            {
				AudEvent_Spit.Clips.Add(bundle.LoadAsset<AudioClip>($"sfx_Paper_Blow_0{i}"));
			}
			AudEvent_Spit.VolumeRange = new Vector2(0.3f, 0.3f);

			//-----------------------------------------------------------

			biteRadius = Config.Bind("Pin Bite Settings",
									 "Bite Radius",
									 0.15f,
									 "How close to your mouth the pin needs to be to be considered bitten");

			forceRequiredForPull = Config.Bind("Pin Bite Settings",
											   "Force Required For Pull",
											   0.01f,
											   "How fast the grenade needs to be moved from the mouth for its pin to be considered bitten");

			pinPullToothChance = Config.Bind("Pin Bite Settings",
											 "Pin Pull Accident Probability (1 is 100%)",
											 0.025f,
											 "After all, those pins are held in there awfully tight...");
		}

        private void PinnedGrenade_UpdateInteraction(On.FistVR.PinnedGrenade.orig_UpdateInteraction orig, PinnedGrenade self, FVRViveHand hand)
        {
			orig(self, hand);

			//get topmost unpulled ring in list
			PinnedGrenadeRing? curRing = null;

			for (int i = 0; i < self.m_rings.Count; i++)
			{
				if (!self.m_rings[i].HasPinDetached())
				{
					curRing = self.m_rings[i];
					break;
				}
			}
			if (curRing != null)
			{
				Vector3 mouthPos = GM.CurrentPlayerBody.Head.transform.position + GM.CurrentPlayerBody.Head.transform.up * -0.2f;
				//bite onto pin if not already biting a different pin
				if (Vector3.Distance(curRing.transform.position, mouthPos) < biteRadius.Value)
				{
					if (!isPinInMouth)
					{
						curBittenRing = curRing;
						isPinInMouth = true;
					}

					if (Vector3.Distance(curRing.transform.position, prevFramePinPos) > forceRequiredForPull.Value)
					{
						//pin bitten and grenade pulled away from mouth, release pin
						if (curRing == curBittenRing && !curBittenRing.HasPinDetached())
						{
							BiteOutPin(curBittenRing);

							//tooth easter egg
							if (pinPullToothChance.Value > 0f)
							{
								if (UnityEngine.Random.Range(0f, 1f) <= pinPullToothChance.Value)
								{
									//spit out tooth
									SpitOutTooth(mouthPos);
								}
							}
						}
					}
				}
				else if (isPinInMouth && pinInMouth == null)
				{
					isPinInMouth = false;
					curBittenRing = null;
				}

				prevFramePinPos = curRing.transform.position;
			}
			//count down to spitting out the pin
            if (isPinInMouth && timeUntilSpitOut > 0f)
			{
				timeUntilSpitOut -= Time.deltaTime;
				if (timeUntilSpitOut <= 0f && pinInMouth != null)
				{
					SpitOutPin(pinInMouth);
					timeUntilSpitOut = UnityEngine.Random.Range(0.4f, 0.6f);
                }
            }
        }

		void BiteOutPin(PinnedGrenadeRing _ring)   //altered varsion of the DetachPin function in-game, separate to make manual pulls still possible
		{
			if (_ring.m_hasPinDetached)
			{
				return;
			}

			curBittenRing = null;
			pinInMouth = _ring.Pin;

			//disable gravity until pin is spat out
			_ring.m_hasPinDetached = true;
			_ring.Pin.RootRigidbody = _ring.Pin.gameObject.AddComponent<Rigidbody>();
			_ring.Pin.RootRigidbody.mass = 0.02f;
			_ring.Pin.RootRigidbody.isKinematic = true;

			_ring.transform.SetParent(_ring.Pin.transform);
			_ring.Pin.transform.SetParent(GM.CurrentPlayerBody.Head);

			_ring.Pin.enabled = true;
			SM.PlayCoreSound(FVRPooledAudioType.GenericClose, _ring.G.AudEvent_Pinpull, _ring.G.transform.position);
			_ring.GetComponent<Collider>().enabled = false;
			_ring.enabled = false;
		}

		void SpitOutPin(FVRPhysicalObject _pin)
		{
			_pin.transform.SetParent(null);
			_pin.RootRigidbody.isKinematic = false;

			Vector3 mouthPos = GM.CurrentPlayerBody.Head.transform.position + GM.CurrentPlayerBody.Head.transform.up * -0.2f;
			SM.PlayGenericSound(AudEvent_Spit, mouthPos);

			Rigidbody rb = _pin.RootRigidbody;
            rb.velocity = GM.CurrentPlayerBody.Head.forward * UnityEngine.Random.Range(1f, 2f) + UnityEngine.Random.onUnitSphere;
			rb.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 5f);

			pinInMouth = null;
			isPinInMouth = false;
		}

		void SpitOutTooth(Vector3 _mouthPos)
        {
			GameObject tooth = Instantiate(toothPrefab, _mouthPos, UnityEngine.Random.rotation);
			Rigidbody rb = tooth.GetComponent<Rigidbody>();

			rb.velocity = GM.CurrentPlayerBody.Head.forward * UnityEngine.Random.Range(2f, 4f) + UnityEngine.Random.onUnitSphere;
			rb.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 5f);

			//play meaty thwack
			//no chuckle
			SM.PlayBulletImpactHit(BulletImpactSoundType.Meat, _mouthPos, 0.7f, 0.8f);
		}
		private void GM_InitScene(On.FistVR.GM.orig_InitScene orig, GM self)
		{
			orig(self);

			//reset all values
			isPinInMouth = false;
			curBittenRing = null;
			pinInMouth = null;
			timeUntilSpitOut = 0.5f;
		}
	}
}