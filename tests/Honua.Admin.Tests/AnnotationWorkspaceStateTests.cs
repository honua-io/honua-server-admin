using System;
using System.Linq;
using Honua.Admin.Models.Annotations;
using Honua.Admin.Services.Annotations;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class AnnotationWorkspaceStateTests
{
    [Fact]
    public void PlaceDraftAnnotation_uses_active_layer_tool_and_style()
    {
        var state = new AnnotationWorkspaceState();

        state.SelectTool(AnnotationTool.Rectangle);
        state.SetStrokeColor("#2563eb");
        var shape = state.PlaceDraftAnnotation();

        Assert.NotNull(shape);
        Assert.Equal(AnnotationTool.Rectangle, shape.Tool);
        Assert.Equal(state.ActiveLayerId, shape.LayerId);
        Assert.Equal("#2563eb", shape.Style.StrokeColor);
        Assert.Equal(shape.Id, state.SelectedShapeId);
    }

    [Fact]
    public void CommunityEdition_blocks_sixth_comment()
    {
        var state = new AnnotationWorkspaceState();
        state.SetEdition(AnnotationEdition.Community);

        for (var i = state.CommentCount; i < state.CommentLimit; i++)
        {
            Assert.NotNull(state.AddComment($"comment {i}", guest: false));
        }

        var blocked = state.AddComment("too many", guest: false);

        Assert.Null(blocked);
        Assert.Contains("5 comments", state.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuestComment_enters_pending_moderation_in_enterprise()
    {
        var state = new AnnotationWorkspaceState();
        state.SetEdition(AnnotationEdition.Enterprise);

        var thread = state.AddComment("Needs owner approval", guest: true);

        Assert.NotNull(thread);
        Assert.Equal(AnnotationCommentStatus.Pending, thread.Status);

        state.ApproveThread(thread.Id);

        Assert.Equal(AnnotationCommentStatus.Open, state.Threads.Single(candidate => candidate.Id == thread.Id).Status);
    }

    [Fact]
    public void AddReply_requires_threaded_comment_edition()
    {
        var state = new AnnotationWorkspaceState();
        var thread = state.Threads[0];

        state.SetEdition(AnnotationEdition.Community);
        state.AddReply(thread.Id, "Community reply");

        Assert.Single(state.Threads.Single(candidate => candidate.Id == thread.Id).Comments);
        Assert.Contains("Pro or Enterprise", state.LastError, StringComparison.Ordinal);

        state.SetEdition(AnnotationEdition.Pro);
        state.AddReply(thread.Id, "Pro reply");

        Assert.Equal(2, state.Threads.Single(candidate => candidate.Id == thread.Id).Comments.Count);
    }

    [Fact]
    public void SaveAndLoadSet_restores_saved_snapshot()
    {
        var state = new AnnotationWorkspaceState();
        var before = state.Shapes.Count;
        var saved = state.SaveCurrentSet("review checkpoint");

        state.SelectTool(AnnotationTool.Arrow);
        state.PlaceDraftAnnotation();
        Assert.True(state.Shapes.Count > before);

        state.LoadSet(saved.Id);

        Assert.Equal("review checkpoint", state.CurrentSetName);
        Assert.Equal(before, state.Shapes.Count);
    }

    [Fact]
    public void ExportGeoJson_includes_visible_annotation_features()
    {
        var state = new AnnotationWorkspaceState();
        state.SelectTool(AnnotationTool.Text);
        state.PlaceDraftAnnotation();

        var export = state.ExportGeoJson();

        Assert.Equal("application/geo+json", export.MimeType);
        Assert.Contains("\"FeatureCollection\"", export.Content, StringComparison.Ordinal);
        Assert.Contains("\"Text\"", export.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfExport_is_enterprise_gated()
    {
        var state = new AnnotationWorkspaceState();
        state.SetEdition(AnnotationEdition.Pro);

        Assert.Null(state.ExportPdf());
        Assert.Contains("Enterprise", state.LastError, StringComparison.Ordinal);

        state.SetEdition(AnnotationEdition.Enterprise);
        var export = state.ExportPdf();

        Assert.NotNull(export);
        Assert.Equal("application/pdf", export.MimeType);
        Assert.StartsWith("%PDF-1.4", export.Content, StringComparison.Ordinal);
    }
}
