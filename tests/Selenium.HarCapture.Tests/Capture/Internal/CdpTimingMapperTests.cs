using FluentAssertions;
using Selenium.HarCapture.Capture.Internal;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public class CdpTimingMapperTests
{
    [Fact]
    public void AllPositiveFields_MapsCorrectly()
    {
        // Arrange: Fresh HTTPS connection with all timing phases present
        // All values in milliseconds relative to requestTime, except requestTime/responseReceivedTime in seconds
        double dnsStart = 0;
        double dnsEnd = 10;
        double connectStart = 10;
        double connectEnd = 30;
        double sslStart = 15;
        double sslEnd = 30;
        double sendStart = 30;
        double sendEnd = 31;
        double receiveHeadersEnd = 50;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.060;

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        result.Blocked.Should().Be(0); // dnsStart is first positive timing
        result.Dns.Should().Be(10); // dnsEnd - dnsStart
        result.Connect.Should().Be(20); // connectEnd - connectStart
        result.Ssl.Should().Be(15); // sslEnd - sslStart
        result.Send.Should().Be(1); // sendEnd - sendStart
        result.Wait.Should().Be(19); // receiveHeadersEnd - sendEnd
        result.Receive.Should().Be(10); // (responseReceivedTime - requestTime) * 1000 - receiveHeadersEnd = 60 - 50
    }

    [Fact]
    public void NegativeFields_ProducesNullOptionals()
    {
        // Arrange: Cached/reused connection - DNS and connect are -1
        double dnsStart = -1;
        double dnsEnd = -1;
        double connectStart = -1;
        double connectEnd = -1;
        double sslStart = -1;
        double sslEnd = -1;
        double sendStart = 0;
        double sendEnd = 1;
        double receiveHeadersEnd = 20;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.025;

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        result.Blocked.Should().Be(0); // sendStart is first positive
        result.Dns.Should().BeNull(); // -1 means not applicable
        result.Connect.Should().BeNull(); // -1 means not applicable
        result.Ssl.Should().BeNull(); // -1 means not applicable
        result.Send.Should().Be(1); // sendEnd - sendStart
        result.Wait.Should().Be(19); // receiveHeadersEnd - sendEnd
        result.Receive.Should().Be(5); // (1000.025 - 1000.0) * 1000 - 20 = 25 - 20
    }

    [Fact]
    public void NullEquivalent_ProducesDefaults()
    {
        // Arrange: All fields -1 (no timing data available)
        double dnsStart = -1;
        double dnsEnd = -1;
        double connectStart = -1;
        double connectEnd = -1;
        double sslStart = -1;
        double sslEnd = -1;
        double sendStart = -1;
        double sendEnd = -1;
        double receiveHeadersEnd = -1;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.0;

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        result.Blocked.Should().BeNull(); // All starts are -1
        result.Dns.Should().BeNull();
        result.Connect.Should().BeNull();
        result.Ssl.Should().BeNull();
        result.Send.Should().Be(0); // Default when -1
        result.Wait.Should().Be(0); // Default when -1
        result.Receive.Should().Be(0); // Default when -1
    }

    [Fact]
    public void SslIncludedInConnect_NotAdditive()
    {
        // Arrange: Verify SSL time is part of connect time (not added separately)
        // connectEnd - connectStart includes the SSL negotiation time
        double dnsStart = 0;
        double dnsEnd = 10;
        double connectStart = 10;
        double connectEnd = 50;
        double sslStart = 20;
        double sslEnd = 50;
        double sendStart = 50;
        double sendEnd = 51;
        double receiveHeadersEnd = 70;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.080;

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        result.Connect.Should().Be(40); // connectEnd - connectStart = 50 - 10
        result.Ssl.Should().Be(30); // sslEnd - sslStart = 50 - 20
        // HAR total time is: blocked + dns + connect + send + wait + receive
        // SSL is NOT added separately to total
        double totalTime = (result.Blocked ?? 0) + (result.Dns ?? 0) + (result.Connect ?? 0) +
                          result.Send + result.Wait + result.Receive;
        totalTime.Should().Be(0 + 10 + 40 + 1 + 19 + 10); // 80ms total
    }

    [Fact]
    public void ReceiveTime_CalculatedFromResponseReceivedTime()
    {
        // Arrange: Verify receive time calculation
        // receive = (responseReceivedTime - requestTime) * 1000 - receiveHeadersEnd
        double dnsStart = -1;
        double dnsEnd = -1;
        double connectStart = -1;
        double connectEnd = -1;
        double sslStart = -1;
        double sslEnd = -1;
        double sendStart = 0;
        double sendEnd = 1;
        double receiveHeadersEnd = 50;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.100;

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        result.Receive.Should().Be(50); // (1000.100 - 1000.0) * 1000 - 50 = 100 - 50 = 50
    }

    [Fact]
    public void ReceiveTime_NonNegative()
    {
        // Arrange: When responseReceivedTime is very close to requestTime, receive should be clamped to 0
        double dnsStart = -1;
        double dnsEnd = -1;
        double connectStart = -1;
        double connectEnd = -1;
        double sslStart = -1;
        double sslEnd = -1;
        double sendStart = 0;
        double sendEnd = 1;
        double receiveHeadersEnd = 50;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.040; // Only 40ms elapsed

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        // (1000.040 - 1000.0) * 1000 - 50 = 40 - 50 = -10, but should be clamped to 0
        result.Receive.Should().Be(0);
    }

    [Fact]
    public void BlockedTime_UsesFirstPositiveStart()
    {
        // Arrange: When dnsStart is -1 but connectStart is positive
        double dnsStart = -1;
        double dnsEnd = -1;
        double connectStart = 5;
        double connectEnd = 25;
        double sslStart = -1;
        double sslEnd = -1;
        double sendStart = 25;
        double sendEnd = 26;
        double receiveHeadersEnd = 45;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.050;

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        result.Blocked.Should().Be(5); // connectStart is first positive
    }

    [Fact]
    public void BlockedTime_UsesSendStart_WhenOthersNegative()
    {
        // Arrange: When both dnsStart and connectStart are -1, blocked = sendStart
        double dnsStart = -1;
        double dnsEnd = -1;
        double connectStart = -1;
        double connectEnd = -1;
        double sslStart = -1;
        double sslEnd = -1;
        double sendStart = 3;
        double sendEnd = 4;
        double receiveHeadersEnd = 23;
        double requestTime = 1000.0;
        double responseReceivedTime = 1000.025;

        // Act
        var result = CdpTimingMapper.MapToHarTimings(
            dnsStart, dnsEnd,
            connectStart, connectEnd,
            sslStart, sslEnd,
            sendStart, sendEnd,
            receiveHeadersEnd,
            requestTime,
            responseReceivedTime);

        // Assert
        result.Blocked.Should().Be(3); // sendStart is first positive
    }
}
