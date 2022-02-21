﻿
using System.Collections.Generic;
using ECSAtmos.Editor;
using UnityEditor;
using UnityEngine;

public static class GizmoUtils
{
	public static Vector3 HalfOne = new Vector3(0.5f, 0.5f, 0);

	public static void DrawGizmos(List<Check> checks, bool local=true)
	{
		float camDistance;
		BoundsInt bounds;
		InitDrawingArea(out camDistance, out bounds, local);
		
		if (camDistance <= 100f)
		{
			DrawGizmos(bounds, checks);
		}

		if (camDistance <= 50f)
		{
			DrawLabels(bounds, checks);
		}
	}
	
	private static void InitDrawingArea(out float camDistance, out BoundsInt bounds, bool local)
	{
		const float margin = 256;

		Vector3 screenBegin = Vector3.one * -margin;
		Vector3 screenEnd = new Vector3(Camera.current.pixelWidth + margin, Camera.current.pixelHeight + margin);

		Vector3 worldPointBegin = Camera.current.ScreenToWorldPoint(screenBegin);
		Vector3 worldPointEnd = Camera.current.ScreenToWorldPoint(screenEnd);

		Vector3Int localStart;
		Vector3Int localEnd;

		localStart = worldPointBegin.RoundToInt();
		localEnd = worldPointEnd.RoundToInt();

		localStart.z = 0;
		localEnd.z = 1;

		camDistance = localEnd.y - localStart.y;

		bounds = new BoundsInt(localStart, localEnd - localStart);
	}

	private static void DrawGizmos(BoundsInt bounds, IReadOnlyCollection<Check> checks)
	{
		foreach (Check check in checks)
		{
			if (check.Active)
			{
				check.DrawGizmo(bounds);
			}
		}
	}

	private static void DrawLabels(BoundsInt bounds, IReadOnlyCollection<Check> checks)
	{
		foreach (Check check in checks)
		{
			if (check.Active)
			{
				check.DrawLabel(bounds);
			}
		}
	}

	public static void DrawCube(Vector3 position, Color color, bool local=true, float alpha = 0.5f, float size = 1)
	{
		color.a = alpha;
		Gizmos.color =color;
		Gizmos.DrawCube(position + (local ? HalfOne: Vector3.zero), Vector3.one * size);
	}

	public static void DrawWireCube(Vector3 position, Color color, bool local=true, float alpha = 0.5f, float size = 1)
	{
		color.a = alpha;
		Gizmos.color =color;
		Gizmos.DrawWireCube(position + (local ? HalfOne: Vector3.zero), Vector3.one * size);
	}

	public static void DrawRay(Vector3 pos, Vector3 direction, bool local=true)
	{
		if (direction == Vector3.zero)
		{
			return;
		}

		if ( local )
		{
			pos += HalfOne;
		}

		Gizmos.DrawRay(pos, direction);
	}

	public static void DrawArrow(Vector3 pos, Vector3 direction, bool local=true, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
	{
		if (direction == Vector3.zero)
		{
			return;
		}

		if ( local )
		{
			pos += HalfOne;
		}

		Gizmos.DrawRay(pos, direction);

		Quaternion lookRotation = Quaternion.LookRotation(direction);
		Vector3 right = lookRotation * Quaternion.Euler(arrowHeadAngle, 0, 0) * Vector3.back;
		Vector3 left = lookRotation * Quaternion.Euler(-arrowHeadAngle, 0, 0) * Vector3.back;
		Vector3 up = lookRotation * Quaternion.Euler(0, arrowHeadAngle, 0) * Vector3.back;
		Vector3 down = lookRotation * Quaternion.Euler(0, -arrowHeadAngle, 0) * Vector3.back;
		Gizmos.color = Gizmos.color;
		Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
		Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
		Gizmos.DrawRay(pos + direction, up * arrowHeadLength);
		Gizmos.DrawRay(pos + direction, down * arrowHeadLength);
	}

	public static void DrawText(string text, Vector3 position, bool local=true, int fontSize = 0, float yOffset = 0)
	{
		DrawText(text, position, Color.white, local, fontSize, yOffset);
	}

	public static void DrawText(string text, Vector3 position, Color color, bool local=true, int fontSize = 0, float yOffset = 0)
	{
		GUISkin guiSkin = GUI.skin;
		if (guiSkin == null)
		{
			Debug.LogWarning("editor warning: guiSkin parameter is null");
			return;
		}

		GUIContent textContent = new GUIContent(text);

		GUIStyle style = new GUIStyle(guiSkin.GetStyle("Label")) {normal = {textColor = color}};

		if (fontSize > 0)
		{
			style.fontSize = fontSize;
			style.fontStyle = FontStyle.Bold;
		}

		Vector2 textSize = style.CalcSize(textContent);

		if (local)
		{
			position += Vector3.one;
		}


		Vector3 screenPoint = Camera.current.WorldToScreenPoint(position);

		float x = screenPoint.x - textSize.x * 0.5f;
		float y = screenPoint.y + textSize.y * 0.5f + yOffset;
		float z = screenPoint.z;

		Vector3 worldPosition = Camera.current.ScreenToWorldPoint(new Vector3(x, y, z));
		Handles.Label(worldPosition, textContent, style);
	}
	
	public static Vector3Int RoundToInt(this Vector3 other)
	{
		return Vector3Int.RoundToInt(other);
	}
}