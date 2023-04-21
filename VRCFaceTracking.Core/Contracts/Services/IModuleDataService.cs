﻿using VRCFaceTracking.Core.Models;

namespace VRCFaceTracking.Core.Contracts.Services;

public interface IModuleDataService
{
    Task<IEnumerable<RemoteTrackingModule>> GetListDetailsDataAsync();
    Task<int> GetMyRatingAsync(RemoteTrackingModule module);
    Task SetMyRatingAsync(RemoteTrackingModule module, int rating);
    IEnumerable<LocalTrackingModule> GetInstalledModules();
    Task IncrementDownloadsAsync(RemoteTrackingModule module);
    IEnumerable<string> GetLegacyModules();
}