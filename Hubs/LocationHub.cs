using LISDBACKEND.Models;
using LISD.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Collections.Concurrent;

namespace LISDBACKEND.Hubs
{
    public class LocationHub : Hub
    {
        private readonly IConfiguration _configuration;
        private readonly SseNotificationService _sseService;
        private static readonly ConcurrentDictionary<int, bool> LiveNotifiedFieldBoys = new();

        public LocationHub(
            IConfiguration configuration,
            SseNotificationService sseService)
        {
            _configuration = configuration;
            _sseService = sseService;
        }

        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            Console.WriteLine($"Admin joined: {Context.ConnectionId}");
        }

        public async Task LeaveAdminGroup()
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Admins");
            Console.WriteLine($"Admin left: {Context.ConnectionId}");
        }

        public async Task<object> JoinFieldBoyLive(int fieldBoyId)
        {
            try
            {
                if (fieldBoyId <= 0)
                {
                    return new
                    {
                        status = false,
                        message = "Invalid FieldBoyId"
                    };
                }

                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"FieldBoy_{fieldBoyId}"
                );

                bool shouldSendNotification = LiveNotifiedFieldBoys.TryAdd(fieldBoyId, true);

                if (shouldSendNotification)
                {
                    await Clients.Group("Admins").SendAsync(
                        "FieldBoyConnected",
                        new
                        {
                            FieldBoyId = fieldBoyId,
                            Message = "Field boy is live now",
                            ConnectedAt = DateTime.Now
                        }
                    );
                    await _sseService.SendToAdminsAsync(new
                    {
                        type = "FIELD_BOY_LIVE",
                        message = "Field boy is live now",
                        fieldBoyId = fieldBoyId,
                        connectedAt = DateTime.Now
                    });
                    Console.WriteLine($"Notification sent one time for field boy: {fieldBoyId}");
                }
                else
                {
                    Console.WriteLine($"Notification already sent for field boy: {fieldBoyId}");
                }

                Console.WriteLine($"Field boy live: {fieldBoyId}");
                return new
                {
                    status = true,
                    message = "Field boy joined live tracking"
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    status = false,
                    message = ex.Message
                };
            }
        }

        public async Task<object> SendLocation(FieldBoyLocationDto location)
        {
            try
            {
                Console.WriteLine("================================");
                Console.WriteLine("Location Received");
                Console.WriteLine($"FieldBoyId: {location.FieldBoyId}");
                Console.WriteLine($"Latitude: {location.Latitude}");
                Console.WriteLine($"Longitude: {location.Longitude}");
                Console.WriteLine($"AccuracyMeters: {location.AccuracyMeters}");
                Console.WriteLine($"CapturedAt: {location.CapturedAt}");
                Console.WriteLine("================================");

                if (location.FieldBoyId <= 0)
                {
                    return new
                    {
                        status = false,
                        message = "Invalid FieldBoyId"
                    };
                }

                using SqlConnection con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection")
                );

                using SqlCommand cmd = new SqlCommand("I_FieldBoyLocationHistory", con);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("@FieldBoyId", SqlDbType.Int).Value =
                    location.FieldBoyId;

                cmd.Parameters.Add("@Latitude", SqlDbType.Decimal).Value =
                    Convert.ToDecimal(location.Latitude);

                cmd.Parameters.Add("@Longitude", SqlDbType.Decimal).Value =
                    Convert.ToDecimal(location.Longitude);

                cmd.Parameters.Add("@AccuracyMeters", SqlDbType.Decimal).Value =
                    Convert.ToDecimal(location.AccuracyMeters);

                cmd.Parameters.Add("@CapturedAtUtc", SqlDbType.DateTime2).Value =
                    !string.IsNullOrWhiteSpace(location.CapturedAt)
                        ? DateTime.Parse(location.CapturedAt).ToUniversalTime()
                        : DateTime.UtcNow;

                await con.OpenAsync();

                FieldBoyLocationDto savedLocation = location;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        savedLocation = new FieldBoyLocationDto
                        {
                            FieldBoyId = Convert.ToInt32(reader["FieldBoyId"]),
                            Latitude = Convert.ToDouble(reader["Latitude"]),
                            Longitude = Convert.ToDouble(reader["Longitude"]),
                            AccuracyMeters = reader["AccuracyMeters"] == DBNull.Value
                                ? 0
                                : Convert.ToDouble(reader["AccuracyMeters"]),
                            CapturedAt = Convert.ToDateTime(reader["CapturedAtUtc"])
                                .ToUniversalTime()
                                .ToString("o")
                        };
                    }
                }

                Console.WriteLine("Location saved successfully");

                await Clients.Group("Admins").SendAsync(
                    "ReceiveLocation",
                    savedLocation
                );

                string fieldBoyName = "";

                using (SqlCommand nameCmd = new SqlCommand(
                    "SELECT FieldBoyName FROM FieldBoyMaster WHERE FieldBoyId = @FieldBoyId",
                    con))
                {
                    nameCmd.Parameters.AddWithValue(
                        "@FieldBoyId",
                        savedLocation.FieldBoyId
                    );

                    var result = await nameCmd.ExecuteScalarAsync();

                    if (result != null)
                    {
                        fieldBoyName = result.ToString();
                    }
                }

                await _sseService.SendToAdminsAsync(new
                {
                    type = "LOCATION_SHARED",
                    message = $"{fieldBoyName} shared live location successfully",
                    fieldBoyId = savedLocation.FieldBoyId,
                    fieldBoyName = fieldBoyName,
                    latitude = savedLocation.Latitude,
                    longitude = savedLocation.Longitude,
                    accuracyMeters = savedLocation.AccuracyMeters,
                    capturedAt = savedLocation.CapturedAt,
                    time = DateTime.Now
                });
                Console.WriteLine("Location broadcasted to admins using SignalR and SSE");
                return new
                {
                    status = true,
                    message = "Location saved and broadcasted",
                    data = savedLocation
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("================================");
                Console.WriteLine("SignalR Location Error");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("================================");

                return new
                {
                    status = false,
                    message = ex.Message
                };
            }
        }
    }
}