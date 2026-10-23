using System;
using System.Collections.Generic;
using System.Linq;
using Eirora.Client.Look;
using Eirora.Simulation;
using Eirora.Simulation.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Eirora.Client.Tests
{
    /// <summary>Looks put together as data (ADR-0009): every building and nature kind has one, and stages build on each other.</summary>
    public sealed class LookTests
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void DestroyCreated()
        {
            foreach (var created in _created)
            {
                UnityEngine.Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void EveryBuildingHasALookForEveryLevelItCanReach()
        {
            var looks = Looks.Load();

            foreach (var kind in ContentCatalog.Current.Buildings)
            {
                var look = looks.For(kind);
                Assert.That(look, Is.Not.Null, kind.ToString());
                Assert.That(look!.levels.Count, Is.EqualTo(ContentCatalog.Current.For(kind).MaxLevel), kind.ToString());
            }
        }

        [Test]
        public void EveryKindOfNatureHasALook()
        {
            var looks = Looks.Load();

            foreach (NatureKind kind in Enum.GetValues(typeof(NatureKind)))
            {
                Assert.That(looks.For(kind), Is.Not.Null, kind.ToString());
                Assert.That(looks.For(kind)!.stages, Is.Not.Empty, kind.ToString());
            }
        }

        [Test]
        public void NoTwoLooksShowTheSameThing()
        {
            var buildings = Resources.LoadAll<BuildingLook>(Looks.Folder).Select(l => l.kind).ToList();
            var nature = Resources.LoadAll<NatureLook>(Looks.Folder).Select(l => l.kind).ToList();

            Assert.That(buildings, Is.Unique);
            Assert.That(nature, Is.Unique);
        }

        [Test]
        public void EveryStageStaysWithinItsTriangleBudget()
        {
            foreach (var look in Resources.LoadAll<StagedLook>(Looks.Folder))
            {
                for (int stage = 0; stage < look.Stages.Count; stage++)
                {
                    var surfaces = new MeshBuilder();
                    var glows = new MeshBuilder();
                    look.Draw(surfaces, glows, stage, Vector3.zero, seed: 0);

                    Assert.That(surfaces.TriangleCount + glows.TriangleCount, Is.InRange(1, look.triangleBudget), $"{look.name}, stage {stage + 1}");
                }
            }
        }

        [Test]
        public void EveryPieceAndModelInALookIsThere()
        {
            foreach (var look in Resources.LoadAll<StagedLook>(Looks.Folder))
            {
                foreach (var part in look.Stages.SelectMany(s => s.parts))
                {
                    if (part.shape == LookShape.Piece)
                    {
                        Assert.That(part.piece != null, $"{look.name}: piece {part.id}");
                    }
                    else if (part.shape == LookShape.Mesh)
                    {
                        Assert.That(part.mesh != null && part.mesh.isReadable, $"{look.name}: model {part.id}");
                    }
                }
            }
        }

        [Test]
        public void ALaterLevelKeepsWhatItBuildsOnAndReplacesPartsByName()
        {
            var look = Building(
                Stage(Part("hall", 1f), Part("shed", 1f)),
                Stage(Part("hall", 2f), Part("tower", 1f)),
                Stage(new[] { "shed" }, Part("annex", 1f)));

            Assert.That(Describe(look.PartsAt(0)), Is.EqualTo(new[] { "hall 1", "shed 1" }));
            Assert.That(Describe(look.PartsAt(1)), Is.EqualTo(new[] { "hall 2", "shed 1", "tower 1" }));
            Assert.That(Describe(look.PartsAt(2)), Is.EqualTo(new[] { "hall 2", "tower 1", "annex 1" }));
        }

        [Test]
        public void AStageThatDoesNotExtendStartsFromNothing()
        {
            var look = Building(Stage(Part("cottage", 1f)), Stage(Part("hall", 1f)));
            look.levels[1].extendsPrevious = false;

            Assert.That(Describe(look.PartsAt(1)), Is.EqualTo(new[] { "hall 1" }));
        }

        [Test]
        public void ALevelWithoutAStageShowsTheLastOne()
        {
            var look = Building(Stage(Part("hut", 1f)), Stage(Part("hut", 2f)));

            Assert.That(Describe(look.PartsAt(BuildingLook.StageOf(3))), Is.EqualTo(new[] { "hut 2" }));
        }

        [TestCase(0f, "Sparse")]
        [TestCase(0.49f, "Sparse")]
        [TestCase(0.5f, "Full")]
        [TestCase(1f, "Full")]
        public void NatureShowsTheStageItsFullnessHasReached(float fullness, string expected)
        {
            var forest = Create<NatureLook>();
            forest.stages.Add(new NatureStage { name = "Sparse", from = 0f });
            forest.stages.Add(new NatureStage { name = "Full", from = 0.5f });

            Assert.That(forest.stages[forest.StageOf(fullness)].name, Is.EqualTo(expected));
        }

        [Test]
        public void ScatteredCopiesOfAPieceKeepTheirPlacesAsTheForestFills()
        {
            var tree = Create<LookPiece>();
            tree.parts.Add(new LookPart { shape = LookShape.Cone, size = new Vector3(0.2f, 0.5f, 0.2f), sides = 5 });
            var sparse = new LookPart { shape = LookShape.Piece, piece = tree, count = 2, spread = 0.7f };
            var full = new LookPart { shape = LookShape.Piece, piece = tree, count = 6, spread = 0.7f };

            var few = Vertices(sparse, seed: 17);
            var many = Vertices(full, seed: 17);

            Assert.That(many.Take(few.Count), Is.EqualTo(few));
            Assert.That(many.All(v => new Vector2(v.x, v.z).magnitude < 0.7f + 0.2f), Is.True);
        }

        [Test]
        public void GlowingPartsShineInsteadOfBeingLit()
        {
            var surfaces = new MeshBuilder();
            var glows = new MeshBuilder();

            new LookPart { shape = LookShape.Cone, glow = true, sides = 4 }.Draw(surfaces, glows, Vector3.zero, 0);
            new LookPart { shape = LookShape.Windows, count = 2 }.Draw(surfaces, glows, Vector3.zero, 0);

            Assert.That(surfaces.TriangleCount, Is.Zero);
            Assert.That(glows.TriangleCount, Is.EqualTo(4 + 4));
        }

        [Test]
        public void AModelMadeByHandStaysFlatAndKeepsItsColours()
        {
            var model = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.forward, Vector3.right, Vector3.up },
                colors = new[] { Color.red, Color.red, Color.red, Color.red },
                triangles = new[] { 0, 1, 2, 0, 3, 1 },
            };
            _created.Add(model);
            var surfaces = new MeshBuilder();

            new LookPart { shape = LookShape.Mesh, mesh = model, size = Vector3.one * 2f }.Draw(surfaces, new MeshBuilder(), Vector3.up, 0);
            var mesh = surfaces.ToMesh("Model");
            _created.Add(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(6), "Each face has its own corners, so its normal stays flat.");
            Assert.That(mesh.colors.Distinct(), Is.EqualTo(new[] { Color.red.linear }));
            Assert.That(mesh.vertices[1], Is.EqualTo(new Vector3(0f, 1f, 2f)));
        }

        [Test]
        public void ABuildingWithoutALookIsDrawnSoItCannotBeMissed()
        {
            var looks = new Looks(Array.Empty<BuildingLook>(), Array.Empty<NatureLook>());
            var surfaces = new MeshBuilder();

            looks.Building(surfaces, new MeshBuilder(), BuildingKind.Sawmill, 1, Vector3.zero, 0);
            var mesh = surfaces.ToMesh("Missing");
            _created.Add(mesh);

            Assert.That(mesh.colors.Distinct(), Is.EqualTo(new[] { Palette.Missing.linear }));
        }

        private static string[] Describe(IEnumerable<LookPart> parts) => parts.Select(p => $"{p.id} {p.size.x}").ToArray();

        private static LookPart Part(string id, float width) => new() { id = id, shape = LookShape.Box, size = new Vector3(width, 0.2f, 0.2f) };

        private static LookStage Stage(params LookPart[] parts) => Stage(Array.Empty<string>(), parts);

        private static LookStage Stage(string[] removes, params LookPart[] parts) => new() { removes = removes.ToList(), parts = parts.ToList() };

        private static List<Vector3> Vertices(LookPart part, int seed)
        {
            var surfaces = new MeshBuilder();
            part.Draw(surfaces, new MeshBuilder(), Vector3.zero, seed);
            var mesh = surfaces.ToMesh("Piece");
            var vertices = mesh.vertices.ToList();
            UnityEngine.Object.DestroyImmediate(mesh);
            return vertices;
        }

        private BuildingLook Building(params LookStage[] levels)
        {
            var look = Create<BuildingLook>();
            look.kind = BuildingKind.FishingHut;
            look.levels = levels.ToList();
            return look;
        }

        private T Create<T>()
            where T : ScriptableObject
        {
            var created = ScriptableObject.CreateInstance<T>();
            _created.Add(created);
            return created;
        }
    }
}
