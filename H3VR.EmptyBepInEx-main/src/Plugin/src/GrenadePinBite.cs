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
		private const string ASSET_BUNDLE_NAME = "brighterpistoollabels";
		GameObject toothPrefab;
		AudioEvent AudEvent_Spit = new();

		public static ConfigEntry<float> pinPullToothChance;

		bool isPinInMouth;
		PinnedGrenadeRing curBittenRing;
		FVRPhysicalObject pinInMouth;

		public GrenadePinBite()
		{
            //hooks here
            On.FistVR.PinnedGrenade.UpdateInteraction += PinnedGrenade_UpdateInteraction;

			//-----------------------------------------------------------

			string pluginPath = Path.GetDirectoryName(Info.Location);
			AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(pluginPath, ASSET_BUNDLE_NAME));
			toothPrefab = bundle.LoadAsset<GameObject>("Tooth");

			AudEvent_Spit.Clips.Add(bundle.LoadAsset<AudioClip>(""));

			pinPullToothChance = Config.Bind("Pin Bite Settings",
											 "Pin Pull Accident Probability (1 = 100%)",
											 0.1f,
											 "After all, those pins are held in there awfully tight...");
		}

		private void PinnedGrenade_UpdateInteraction(On.FistVR.PinnedGrenade.orig_UpdateInteraction orig, PinnedGrenade self, FVRViveHand hand)
        {
			orig(self, hand);

			if (!self.m_isPinPulled)
            {
				Vector3 mouthPos = GM.CurrentPlayerBody.Head.transform.position + GM.CurrentPlayerBody.Head.transform.up * -0.2f;
				if (!isPinInMouth)
                {
					if (Vector3.Distance(self.transform.position, mouthPos) < 0.15f)
					{
						isPinInMouth = true;
                        for (int i = 0; i < self.m_rings.Count; i++)
                        {
							if (!self.m_rings[i].HasPinDetached())
                            {
								curBittenRing = self.m_rings[i];
								break;
                            }
                        }
					}
				}
                else if (Vector3.Distance(self.transform.position, mouthPos) > 0.15f)
                {
					//pin bitten and grenade pulled away from mouth, release pin
					for (int i = 0; i < self.m_rings.Count; i++)
					{
						if (!self.m_rings[i].HasPinDetached() == self.m_rings[i] == curBittenRing)
						{
							BiteOutPin(curBittenRing);
							break;
						}
					}
				}

            }
        }

		void BiteOutPin(PinnedGrenadeRing _ring)   //altered varsion of the DetachPin function in-game, separate to make manual pulls still possible
		{
			curBittenRing = null;
			pinInMouth = _ring.Pin;

			if (_ring.m_hasPinDetached)
			{
				return;
			}
			_ring.m_hasPinDetached = true;

			//hide pin and disable gravity until it is spat out
			_ring.Pin.transform.SetParent(GM.CurrentPlayerBody.Head);
			_ring.Pin.RootRigidbody = _ring.Pin.gameObject.AddComponent<Rigidbody>();
			_ring.Pin.RootRigidbody.mass = 0.02f;
			_ring.Pin.RootRigidbody.isKinematic = true;
			_ring.Pin.enabled = true;

			SM.PlayCoreSound(FVRPooledAudioType.GenericClose, _ring.G.AudEvent_Pinpull, _ring.G.transform.position);

			_ring.GetComponent<Collider>().enabled = false;
			_ring.enabled = false;
		}

		void SpitOutPin(FVRPhysicalObject _pin)
        {
			_pin.RootRigidbody.isKinematic = false;

			Vector3 mouthPos = GM.CurrentPlayerBody.Head.transform.position + GM.CurrentPlayerBody.Head.transform.up * -0.2f;
			SM.PlayGenericSound(AudEvent_Spit, mouthPos);
        }
	}
}