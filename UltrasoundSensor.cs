using System;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.Storage.Streams;
using Windows.Devices.SerialCommunication;

class UltrasoundSensor
{
    GpioController controller; GpioPin trigPin; GpioPin echoPin; ManualResetEventSlim eventSlim; Stopwatch stopwatch;
    Task<double> task; CancellationToken ct; CancellationTokenSource cts;
    UltrasoundSensor(int trig_pin, int echo_pin)
    {
        controller = GpioController.GetDefault();
        trigPin = controller.OpenPin(trig_pin);
        trigPin.SetDriveMode(GpioPinDriveMode.Output);
        echoPin = controller.OpenPin(echo_pin);
        echoPin.SetDriveMode(GpioPinDriveMode.Output);
        trigPin.Write(GpioPinValue.Low);
        eventSlim = new ManualResetEventSlim(false);
        stopwatch = new Stopwatch();
        cts = new CancellationTokenSource();
        ct = cts.Token;
    }

    double distanceV1(int max_wait_time)
    {
        stopwatch.Reset();
        trigPin.Write(GpioPinValue.High);
        eventSlim.Wait(TimeSpan.FromMilliseconds(0.01));
        trigPin.Write(GpioPinValue.Low);
        while (echoPin.Read() == GpioPinValue.Low) { }
        task = Task<double>.Factory.StartNew(() =>
        {
            stopwatch.Start();
            cts.CancelAfter(max_wait_time);
            while (echoPin.Read() == GpioPinValue.High) { }
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalSeconds * 343.2;
        }, ct);
        task.Wait(ct);
        if (task.IsCanceled)
        {
            return -1;
        }
        return task.Result;
    }
    double distanceV2(int max_wait_time)
    {
        stopwatch.Reset();
        trigPin.Write(GpioPinValue.High);
        eventSlim.Wait(TimeSpan.FromMilliseconds(0.01));
        trigPin.Write(GpioPinValue.Low);
        while (echoPin.Read() == GpioPinValue.Low) { }
        task = Task<double>.Factory.StartNew(() =>
        {
            stopwatch.Start();
            while (echoPin.Read() == GpioPinValue.High) { }
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalSeconds * 343.2;
        }, ct);
        task.Wait(max_wait_time);
        if (task.IsCanceled)
        {
            return -1;
        }
        return task.Result;
    }
}