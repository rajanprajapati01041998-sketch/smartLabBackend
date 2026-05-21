using LISDBACKEND.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System.Data;

namespace LISDBACKEND.Hubs
{
    public class LocationHub : Hub
    {
        private readonly IConfiguration _configuration;

        public LocationHub(IConfiguration configuration)
        {
            _configuration = configuration;
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

                await Clients.Group("Admins").SendAsync(
                    "FieldBoyConnected",
                    new
                    {
                        FieldBoyId = fieldBoyId,
                        Message = "Field boy is live now",
                        ConnectedAt = DateTime.Now
                    }
                );

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
                    return new { status = false, message = "Invalid FieldBoyId" };
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

                using var reader = await cmd.ExecuteReaderAsync();

                FieldBoyLocationDto savedLocation = location;

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

                Console.WriteLine("Location saved successfully");

                await Clients.Group("Admins").SendAsync(
                    "ReceiveLocation",
                    savedLocation
                );

                Console.WriteLine("Location broadcasted to admins");

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