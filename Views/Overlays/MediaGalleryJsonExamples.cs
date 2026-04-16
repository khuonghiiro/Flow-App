namespace FlowMy.Views.Overlays
{
    /// <summary>
    /// Chứa ví dụ JSON chuẩn cho MediaGallery (Grid & Grouped) để hiển thị trong dialog và cho phép copy nhanh.
    /// </summary>
    public static class MediaGalleryJsonExamples
    {
        public static string GridExample =>
@"{
  ""media"": [
    {
      ""title"": ""Ảnh 1"",
      ""imageUrl"": ""https://example.com/image1.jpg"",
      ""videoUrl"": ""https://example.com/video1.mp4""
    },
    {
      ""title"": ""Ảnh 2"",
      ""imageUrl"": ""https://example.com/image2.jpg""
    }
  ]
}";

        public static string GroupedExample =>
@"{
  ""workflows"": [
    {
      ""workflowId"": ""wf-1"",
      ""videos"": [
        {
          ""title"": ""Ảnh 1"",
          ""imageUrl"": ""https://example.com/image1.jpg"",
          ""videoUrl"": ""https://example.com/video1.mp4""
        },
        {
          ""title"": ""Ảnh 2"",
          ""imageUrl"": ""https://example.com/image2.jpg""
        }
      ]
    },
    {
      ""workflowId"": ""wf-2"",
      ""videos"": [
        {
          ""title"": ""Ảnh 3"",
          ""imageUrl"": ""https://example.com/image3.jpg""
        }
      ]
    }
  ]
}";
    }
}
