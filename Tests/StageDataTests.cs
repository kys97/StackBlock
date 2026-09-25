using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class StageDataTests
{
    [Test]
    public void AllNineAssetsPreserveSettingsAndReferenceAssets()
    {
        var assets = Resources.LoadAll<StageData>("StageData");
        Assert.AreEqual(9, assets.Length);
        var stages = new System.Collections.Generic.HashSet<GameManager.Stage>();
        foreach (StageData data in assets)
        {
            Assert.IsTrue(stages.Add(data.Stage), "Unique stage: " + data.name);
            Assert.AreEqual(data.Stage.ToString(), data.name);
            bool weather = (int)data.Stage < (int)GameManager.Stage.Bigben;
            Assert.AreEqual(weather ? GameManager.Topic.Weather : GameManager.Topic.Structure, data.Topic);
            Assert.AreEqual(20f, data.TimeLimitSeconds);
            Assert.AreEqual(5f, data.TimeBonusSeconds);
            Assert.AreEqual(50, data.PointsPerPiece);
            Assert.AreEqual(90f, data.SnapDistancePixels);
            Assert.AreEqual(weather ? new Vector3(0.22f, 1.5f, -3f) : new Vector3(0.3f, 2f, -3f), data.InitialCameraPosition);
            Assert.AreEqual(new Vector3(weather ? 23f : 32f, 0, 0), data.InitialCameraEulerAngles);
            Assert.IsNotNull(data.PreviewImage, data.name);
            Assert.AreEqual(data.name, data.PreviewImage.name.Substring(1));
            Assert.IsNotNull(data.GroundPrefab, data.name);
            Assert.AreSame(Resources.Load<GameObject>("Ground/" + data.name), data.GroundPrefab);
            Assert.IsFalse(data.GroundPrefab.scene.IsValid(), "Ground must be an asset, not a scene object");
        }
    }

    [Test]
    public void CentralLookupCachesSelectionAndRefreshesWhenStageChanges()
    {
        var owner = new GameObject("Stage data lookup test");
        try
        {
            var manager = owner.AddComponent<GameManager>();
            foreach (GameManager.Stage stage in Enum.GetValues(typeof(GameManager.Stage)))
            {
                manager.Session.Stage = stage;
                StageData data = manager.CurrentStageData;
                Assert.AreEqual(stage, data.Stage);
                Assert.AreSame(data, manager.CurrentStageData);
                Assert.AreSame(data.PreviewImage, manager.CurrentStageData.PreviewImage);
                Assert.AreEqual(stage, manager.Session.Stage);
            }
        }
        finally
        {
            Object.DestroyImmediate(owner);
            foreach (var session in Object.FindObjectsByType<GameSession>(FindObjectsSortMode.None)) Object.DestroyImmediate(session.gameObject);
        }
    }
}
