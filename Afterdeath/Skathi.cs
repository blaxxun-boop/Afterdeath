using System.Collections;
using System.Linq;
using UnityEngine;

namespace Afterdeath;

public class PlayerSpawn : MonoBehaviour
{
	public void Awake()
	{
		IEnumerator WaitForPlayer()
		{
			yield return new WaitWhile(() => !Player.m_localPlayer);
			if (Utils.IsGhost(Player.m_localPlayer) && global::Utils.DistanceXZ(Player.m_localPlayer.transform.position, transform.position) < 10)
			{
				Player.m_localPlayer.transform.position = transform.position;
				if (FindObjectsOfType<Skathi>().OrderBy(p => global::Utils.DistanceXZ(p.transform.position, Player.m_localPlayer.transform.position)).FirstOrDefault() is { } skathi)
				{
					Player.m_localPlayer.SetLookDir(skathi.transform.position - Player.m_localPlayer.transform.position);
				}
				Physics.SyncTransforms();
			}
		}
		StartCoroutine(WaitForPlayer());
	}
}

public class Skathi : MonoBehaviour, Interactable, Hoverable
{
	public void Update()
	{
		if (Player.m_localPlayer?.GetSEMan().HaveStatusEffect(Afterdeath.ghostStatus.NameHash()) == false)
		{
			gameObject.SetActive(false);
			transform.parent.Find("StoneCircle").gameObject.SetActive(true);
		}
		else
		{
			transform.parent.Find("StoneCircle").gameObject.SetActive(false);
		}
	}

	public bool Interact(Humanoid user, bool hold, bool alt)
	{
		if (user is Player player && Utils.CarriesTombstone(player))
		{
			return false;
		}
		
		if (alt)
		{
			UnifiedPopup.Push(new YesNoPopup("$ad_skathi_interact_title_alt", "$ad_skathi_interact_message_alt", () =>
			{
				transform.parent.Find("PlayerSpawnEffect").gameObject.SetActive(true);
				Animator animator = GetComponent<Animator>();
				animator.Play("cast revive");
				IEnumerator AwaitAnimationEnd()
				{
					yield return new WaitForSeconds(2.8f);
					if (user)
					{
						user.GetSEMan().RemoveStatusEffect(Afterdeath.ghostStatus);
						Afterdeath.MoveToSkathi.forceSkipSkathi = true;
						Game.instance.FindSpawnPoint(out _, out _, 0);
						user.TeleportTo(ZNet.instance.GetReferencePosition(), Quaternion.identity, true);
					}
				}
				StartCoroutine(AwaitAnimationEnd());
				UnifiedPopup.Pop();
			}, UnifiedPopup.Pop));
		}
		else
		{
			UnifiedPopup.Push(new YesNoPopup("$ad_skathi_interact_title", "$ad_skathi_interact_message", () =>
			{
				transform.parent.Find("PlayerSpawnEffect").gameObject.SetActive(true);
				Animator animator = GetComponent<Animator>();
				animator.Play("cast revive");
				IEnumerator AwaitAnimationEnd()
				{
					yield return new WaitForSeconds(2.8f);
					if (user)
					{
						user.GetSEMan().RemoveStatusEffect(Afterdeath.ghostStatus);
					}
				}
				StartCoroutine(AwaitAnimationEnd());
				UnifiedPopup.Pop();
			}, UnifiedPopup.Pop));
		}

		return false;
	}

	public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

	public string GetHoverText() => Localization.instance.Localize(Utils.CarriesTombstone(Player.m_localPlayer) ? "$ad_skathi_interact_tombstone" : "[<color=yellow><b>$KEY_Use</b></color>] $ad_skathi_hover_text\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $ad_skathi_hover_text_alt");

	public string GetHoverName() => Localization.instance.Localize("$ad_skathi_hover_name");
}
