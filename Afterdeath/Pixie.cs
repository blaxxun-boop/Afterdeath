using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Afterdeath;

public class Pixie : MonoBehaviour
{
	public Player player = null!;
	public Vector3 deathPoint;
	public bool pixieArrived = false;
	private float duration = 1f;

	public void Update()
	{
		if (pixieArrived)
		{
			return;
		}

		pixieArrived = true;
		Vector3 newPosition = player.transform.position + ((deathPoint - player.transform.position) with { y = 0 }).normalized * Random.Range(8f, 11f);
		Heightmap.GetHeight(newPosition, out float height);
		newPosition.y = Math.Max(player.transform.position.y, height + 1);
		duration = Random.Range(0.5f, 1.5f);

		StartCoroutine(MoveOrb(newPosition));
	}

	private IEnumerator MoveOrb(Vector3 newPosition)
	{
		float timer = 0f;
		Vector3 startPosition = transform.position;
		Quaternion targetRotation = Quaternion.LookRotation((newPosition - startPosition) with { y = 0 });
		if (global::Utils.DistanceXZ(startPosition, newPosition) > 5f)
		{
			duration = 0.5f;
		}
		while (timer < duration)
		{
			timer += Time.deltaTime;
			float tmpTimer = timer / duration;
			tmpTimer = (float)Math.Pow(tmpTimer, 3) * (tmpTimer * (6f * tmpTimer - 15f) + 10f);
			transform.position = Vector3.Lerp(startPosition, newPosition, tmpTimer);
			transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 180f * Time.deltaTime);
			
			yield return null;
		}

		yield return new WaitForSeconds(Random.Range(0.1f, 0.4f));
		pixieArrived = false;
	}
}
