using Newtonsoft.Json;
using System.Globalization;
using System.IO.Ports;
using System.Text;

public class WeatherData
{
    public DateTime TimeStamp { get; set; }
    public string Sensor { get; set; }
    public double WindSpeed { get; set; }
    public double WindDirection { get; set; }
}

public class WeatherSensor
{
    private SerialPort _serialPort;
    private string _portName;
    private string _fileName = "weather_data.json";
    private List<byte> buffer = new List<byte>();

    public WeatherSensor(string portName)
    {
        _portName = portName;
    }

    public async Task StartAsync()
    {
        _serialPort = new SerialPort(_portName, 2400, Parity.None, 8, StopBits.One);
        try
        {
            _serialPort.Open();
            _serialPort.DataReceived += SerialPort_DataReceived;
            Console.WriteLine("Started data acquisition.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening port: {ex.Message}");
        }
    }


    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        int bytesToRead = _serialPort.BytesToRead;
        byte[] receivedBytes = new byte[bytesToRead];
        _serialPort.Read(receivedBytes, 0, bytesToRead);
        buffer.AddRange(receivedBytes);
        ProcessByteBuffer();
    }

    private void ProcessByteBuffer()
    {
        int startIndex = buffer.IndexOf((byte)'$');
        if (startIndex >= 0)
        {
            int endIndex = buffer.IndexOf((byte)'\n', startIndex + 1);
            if (endIndex >= 0)
            {
                byte[] messageBytes = buffer.GetRange(startIndex, endIndex - startIndex + 1).ToArray();
                buffer.RemoveRange(0, endIndex + 1);

                string message = Encoding.ASCII.GetString(messageBytes);

                try
                {
                    string cleanedData = message.Substring(1).Trim();
                    string[] parts = cleanedData.Split(',');

                    if (parts.Length == 2)
                    {
                        string speedStr = parts[0].Trim().Replace(',', '.');
                        string directionStr = parts[1].Trim().Replace(',', '.');

                        if (double.TryParse(speedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double windSpeed) && 
                            double.TryParse(directionStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double windDirection))
                        {
                            WeatherData weatherData = new WeatherData
                            {
                                TimeStamp = DateTime.Now,
                                Sensor = "WMT700",
                                WindSpeed = windSpeed,
                                WindDirection = windDirection
                            };

                            string jsonData = JsonConvert.SerializeObject(weatherData);
                            File.AppendAllText(_fileName, jsonData + Environment.NewLine);
                            Console.WriteLine($"Data recorded: {jsonData}");
                        }
                        else
                        {
                            Console.WriteLine($"Error parsing numbers: {cleanedData}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Invalid data format: {message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing data: {ex.Message} Message: {message}");
                }
            }
        }
    }

    public void Stop()
    {
        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            Console.WriteLine("Stopped data acquisition.");
        }
    }

    public static void Main(string[] args)
    {
        WeatherSensor sensor = new WeatherSensor("COM5");
        sensor.StartAsync().Wait();

        Thread.Sleep(30000);
        sensor.Stop();
    }
}
