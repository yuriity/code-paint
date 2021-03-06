using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using CodePaint.WebApi.Domain.Models;
using CodePaint.WebApi.Domain.Repositories;
using CodePaint.WebApi.Controllers.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace CodePaint.WebApi.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class GalleryController : ControllerBase
    {
        private readonly IGalleryMetadataRepository _metadataRepository;
        private readonly IVSCodeThemeStoreRepository _themeStoreRepository;

        public GalleryController(
            IGalleryMetadataRepository metadataRepository,
            IVSCodeThemeStoreRepository themeStoreRepository)
        {
            _metadataRepository = metadataRepository;
            _themeStoreRepository = themeStoreRepository;
        }

        // GET api/gallery?pageNumber=2&pageSize=10&sortBy=Downloads
        [HttpGet]
        public async Task<QueryResultResource<ExtensionMetadataResource>> Index(
            [FromQuery] GalleryQueryResource queryResource)
        {
            var query = Mapper.Map<GalleryQueryResource, GalleryQuery>(queryResource);

            var queryResult = await _metadataRepository.GetItems(query);

            return Mapper.Map<QueryResult<ExtensionMetadata>, QueryResultResource<ExtensionMetadataResource>>(queryResult);
        }

        // GET api/gallery/id
        [HttpGet("{id}")]
        public async Task<ActionResult<ExtensionResource>> Get(string id)
        {
            var metadata = await _metadataRepository.GetExtensionMetadata(id);
            if (metadata == null)
            {
                Log.Information($"Couldn't find ExtensionMetadata '{id}'.");
                return new NotFoundResult();
            }
            if (metadata.Type != ExtensionType.Default)
            {
                Log.Information($"ExtensionMetadata '{id}' doesn't contain themes (ExtensionType: {metadata.Type})");
                return new NotFoundResult();
            }

            var storedTheme = await _themeStoreRepository.GetTheme(id);
            if (storedTheme == null)
            {
                Log.Error($"Stored theme for '{id}' extension is empty.");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var extension = Mapper.Map<ExtensionMetadata, ExtensionResource>(metadata);
            extension.Themes = ConvertThemes(storedTheme.Themes);

            return new OkObjectResult(extension);
        }

        private List<ThemeResource> ConvertThemes(IEnumerable<Theme> storedThemes)
        {
            return storedThemes
                .Select(theme =>
                {
                    var themeResource = Mapper.Map<Theme, ThemeResource>(theme);
                    themeResource.Rules = theme.TokenColors
                        .Select(tc => Mapper.Map<TokenColor, TokenColorResource>(tc))
                        .ToList();
                    themeResource.Colors = new Dictionary<string, string>(
                        theme.Colors
                            .FindAll(c => !string.IsNullOrWhiteSpace(c.Value))
                            .Select(c => KeyValuePair.Create(c.PropertyName, c.Value)));

                    return themeResource;
                })
                .ToList();
        }
    }
}
