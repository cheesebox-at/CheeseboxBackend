using Microsoft.AspNetCore.Authorization;

namespace Backend.Endpoints;

public class ImageEndpoint
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/image");
        
        group.MapPost("/upload", async (HttpRequest request, IWebHostEnvironment env) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("Invalid content type");
            
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("image");
            
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded");
            
            // Validate file size
            if (file.Length > MaxFileSize)
                return Results.BadRequest($"File size exceeds maximum allowed size of {MaxFileSize / 1024 / 1024}MB");
            
            // Validate file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                return Results.BadRequest($"Invalid file type. Allowed types: {string.Join(", ", AllowedExtensions)}");
            
            // Generate unique filename
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var guid = Guid.NewGuid().ToString("N")[..8];
            var newFileName = $"product_{timestamp}_{guid}{extension}";
            
            // Ensure directory exists
            var uploadPath = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "assets", "products");
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);
            
            var filePath = Path.Combine(uploadPath, newFileName);
            
            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            
            return Results.Ok(new { fileName = newFileName, path = $"/assets/products/{newFileName}" });
        }).RequireAuthorization();
        
        group.MapDelete("/delete", async (string filename, IWebHostEnvironment env) =>
        {
            if (string.IsNullOrWhiteSpace(filename))
                return Results.BadRequest("Filename is required");
            
            // Security: prevent path traversal
            if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
                return Results.BadRequest("Invalid filename");
            
            var filePath = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "assets", "products", filename);
            
            if (!File.Exists(filePath))
                return Results.NotFound();
            
            try
            {
                File.Delete(filePath);
                return Results.Ok();
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to delete file: {ex.Message}");
            }
        }).RequireAuthorization();
    }
}



