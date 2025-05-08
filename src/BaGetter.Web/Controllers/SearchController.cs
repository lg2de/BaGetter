using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Authentication;
using BaGetter.Core;
using BaGetter.Protocol.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BaGetter.Web;

[Authorize(AuthenticationSchemes = AuthenticationConstants.NugetBasicAuthenticationScheme, Policy = AuthenticationConstants.NugetUserPolicy)]
public class SearchController : Controller
{
    private readonly ISearchService _searchService;
    private readonly IUpstreamClient _upstreamClient;


    public SearchController(ISearchService searchService, IUpstreamClient upstreamClient)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _upstreamClient = upstreamClient ?? throw new ArgumentNullException(nameof(upstreamClient));
    }

    public async Task<ActionResult<SearchResponse>> SearchAsync(
        [FromQuery(Name = "q")] string query = null,
        [FromQuery]int skip = 0,
        [FromQuery]int take = 20,
        [FromQuery]bool prerelease = false,
        [FromQuery]string semVerLevel = null,

        // These are unofficial parameters
        [FromQuery]string packageType = null,
        [FromQuery]string framework = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest
        {
            Skip = skip,
            Take = take,
            IncludePrerelease = prerelease,
            IncludeSemVer2 = semVerLevel == "2.0.0",
            PackageType = packageType,
            Framework = framework,
            Query = query ?? string.Empty,
        };

        var response1 = await _searchService.SearchAsync(request, cancellationToken);
        var response2 = await _upstreamClient.SearchAsync(request, cancellationToken);
        var combinedResponse = new SearchResponse
        {
            Context = response1.Context, TotalHits = response1.TotalHits + response2.TotalHits,
            Data = response1.Data.Concat(response2.Data).Distinct().ToList()
        };
        return combinedResponse;
    }

    public async Task<ActionResult<AutocompleteResponse>> AutocompleteAsync(
        [FromQuery(Name = "q")] string autocompleteQuery = null,
        [FromQuery(Name = "id")] string versionsQuery = null,
        [FromQuery]bool prerelease = false,
        [FromQuery]string semVerLevel = null,
        [FromQuery]int skip = 0,
        [FromQuery]int take = 20,

        // These are unofficial parameters
        [FromQuery]string packageType = null,
        CancellationToken cancellationToken = default)
    {
        // If only "id" is provided, find package versions. Otherwise, find package IDs.
        if (versionsQuery != null && autocompleteQuery == null)
        {
            var request = new VersionsRequest
            {
                IncludePrerelease = prerelease,
                IncludeSemVer2 = semVerLevel == "2.0.0",
                PackageId = versionsQuery,
            };

            return await _searchService.ListPackageVersionsAsync(request, cancellationToken);
        }
        else
        {
            var request = new AutocompleteRequest
            {
                IncludePrerelease = prerelease,
                IncludeSemVer2 = semVerLevel == "2.0.0",
                PackageType = packageType,
                Skip = skip,
                Take = take,
                Query = autocompleteQuery,
            };

            return await _searchService.AutocompleteAsync(request, cancellationToken);
        }
    }

    public async Task<ActionResult<DependentsResponse>> DependentsAsync(
        [FromQuery] string packageId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return BadRequest();
        }

        return await _searchService.FindDependentsAsync(packageId, cancellationToken);
    }
}
