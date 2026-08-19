using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;
using Windows.Devices.Geolocation;

namespace DesktopCalendar.App.Services;

public sealed class WindowsLocationService : ICurrentLocationService
{
    public async Task<WeatherLocation> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        var access = await Geolocator.RequestAccessAsync();
        if (access != GeolocationAccessStatus.Allowed)
            throw new InvalidOperationException("Windows 위치 권한이 허용되지 않았습니다. 위치 서비스 설정을 확인하거나 도시를 직접 입력하세요.");

        var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
        var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(15));
        cancellationToken.ThrowIfCancellationRequested();
        var coordinates = position.Coordinate.Point.Position;
        return new WeatherLocation("현재 위치", string.Empty, string.Empty, coordinates.Latitude, coordinates.Longitude, "auto");
    }
}
