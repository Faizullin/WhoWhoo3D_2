using System.Linq;
using NUnit.Framework;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Maps;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Maps
{
    public class MapSerializerTests
    {
        private static MapMetadata Meta => new("Test", "2026-09-07T00:00:00+00:00");

        private const string MinimalMap = @"{
          ""schemaVersion"": 2, ""name"": ""m"", ""createdAt"": ""x"",
          ""cells"": [ { ""id"": ""a"", ""position"": {""x"":0,""y"":0,""z"":0}, ""sideCount"": 6,
            ""joins"": [],
            ""slots"": [
              { ""side"": 0, ""edge"": 0, ""owner"": 1, ""partner"": {""cellId"":""a"",""sideIndex"":2,""edge"":0} },
              { ""side"": 2, ""edge"": 0, ""owner"": 1, ""partner"": {""cellId"":""a"",""sideIndex"":0,""edge"":0} }
            ] } ] }";

        [Test]
        public void RoundTrip_PreservesTopologyAndOwners()
        {
            var board = TestBoards.TwoCubes();
            TestBoards.FillFace(board, new SideId(new CellId("cube_a"), 0), TestBoards.P1);

            var restored = MapSerializer.Deserialize(MapSerializer.Serialize(board, Meta));

            Assert.IsTrue(restored.Validate().IsValid, restored.Validate().ToString());
            Assert.AreEqual(
                board.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount)),
                restored.Cells.Values.Sum(c => c.Sides.Sum(s => s.SlotCount)));
            Assert.AreEqual(4, restored.GetSide(new SideId(new CellId("cube_a"), 0)).OwnedCount(TestBoards.P1));
            Assert.IsTrue(restored.GetSide(new SideId(new CellId("cube_a"), 2)).IsInterior);
        }

        [Test]
        public void Deserialize_AcceptsAMinimalPair()
        {
            var board = MapSerializer.Deserialize(MinimalMap);

            Assert.IsTrue(board.Validate().IsValid);
            Assert.AreEqual(1, board.GetSide(new SideId(new CellId("a"), 0)).SlotCount);
        }

        [Test]
        public void Deserialize_RejectsWrongSchemaVersion()
        {
            var json = MinimalMap.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1");
            Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        }

        [Test]
        public void Deserialize_RejectsMalformedJson()
        {
            Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize("{ not json"));
        }

        [Test]
        public void Deserialize_RejectsDuplicateCellIds()
        {
            var json = MinimalMap.Replace("\"cells\": [ {", "\"cells\": [ { \"id\": \"a\", \"sideCount\": 6, \"slots\": [] }, {");
            Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        }

        [Test]
        public void Deserialize_RejectsASlotWithNoPartnerDeclared()
        {
            var json = MinimalMap.Replace(
                @"{ ""side"": 0, ""edge"": 0, ""owner"": 1, ""partner"": {""cellId"":""a"",""sideIndex"":2,""edge"":0} },",
                @"{ ""side"": 0, ""edge"": 0, ""owner"": 1 },");

            var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
            StringAssert.Contains("pairs", ex.Message);
        }

        [Test]
        public void Deserialize_RejectsALoneSlotWhosePartnerIsNeverCreated()
        {
            // Declares one half only: the partner slot is never listed by the other face.
            const string json = @"{
              ""schemaVersion"": 2, ""name"": ""m"", ""createdAt"": ""x"",
              ""cells"": [ { ""id"": ""a"", ""position"": {""x"":0,""y"":0,""z"":0}, ""sideCount"": 6,
                ""joins"": [], ""slots"": [
                  { ""side"": 0, ""edge"": 0, ""owner"": 0, ""partner"": {""cellId"":""zzz"",""sideIndex"":2,""edge"":0} }
                ] } ] }";

            Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        }

        [Test]
        public void Deserialize_RejectsSlotsOnAnInteriorFace()
        {
            const string json = @"{
              ""schemaVersion"": 2, ""name"": ""m"", ""createdAt"": ""x"",
              ""cells"": [
                { ""id"": ""a"", ""position"": {""x"":0,""y"":0,""z"":0}, ""sideCount"": 6,
                  ""joins"": [ {""side"":2,""cellId"":""b"",""sideIndex"":3} ],
                  ""slots"": [
                    { ""side"": 2, ""edge"": 0, ""owner"": 0, ""partner"": {""cellId"":""a"",""sideIndex"":0,""edge"":0} },
                    { ""side"": 0, ""edge"": 0, ""owner"": 0, ""partner"": {""cellId"":""a"",""sideIndex"":2,""edge"":0} }
                  ] },
                { ""id"": ""b"", ""position"": {""x"":1,""y"":0,""z"":0}, ""sideCount"": 6,
                  ""joins"": [ {""side"":3,""cellId"":""a"",""sideIndex"":2} ], ""slots"": [] } ] }";

            Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        }

        [Test]
        public void Deserialize_RejectsAContestedFace()
        {
            // Two faces each held by a different player is legal; one face holding both is not.
            const string contested = @"{
              ""schemaVersion"": 2, ""name"": ""m"", ""createdAt"": ""x"",
              ""cells"": [ { ""id"": ""a"", ""position"": {""x"":0,""y"":0,""z"":0}, ""sideCount"": 6,
                ""joins"": [], ""slots"": [
                  { ""side"": 0, ""edge"": 0, ""owner"": 1, ""partner"": {""cellId"":""a"",""sideIndex"":2,""edge"":0} },
                  { ""side"": 2, ""edge"": 0, ""owner"": 1, ""partner"": {""cellId"":""a"",""sideIndex"":0,""edge"":0} },
                  { ""side"": 0, ""edge"": 1, ""owner"": 2, ""partner"": {""cellId"":""a"",""sideIndex"":3,""edge"":0} },
                  { ""side"": 3, ""edge"": 0, ""owner"": 2, ""partner"": {""cellId"":""a"",""sideIndex"":0,""edge"":1} }
                ] } ] }";

            var ex = Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(contested));
            StringAssert.Contains("contested", ex.Message);
        }

        [Test]
        public void Deserialize_RejectsNegativeOwner()
        {
            var json = MinimalMap.Replace(@"""owner"": 1, ""partner"": {""cellId"":""a"",""sideIndex"":2", @"""owner"": -3, ""partner"": {""cellId"":""a"",""sideIndex"":2");
            Assert.Throws<MapDeserializationException>(() => MapSerializer.Deserialize(json));
        }

        [Test]
        public void SerializedMapDeclaresSchemaVersionTwo()
        {
            var json = MapSerializer.Serialize(TestBoards.SingleCube(), Meta);
            StringAssert.Contains("\"schemaVersion\": 2", json);
        }
    }
}
