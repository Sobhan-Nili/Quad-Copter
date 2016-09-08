using System;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.Storage.Streams;
using Windows.Devices.SerialCommunication;

class UART_Device
{
    class data
    {
        public List<string> rdata;
    }
    private data cdata;
    SerialDevice device; DataReader reader; DataWriter writer;
    bool is_reading = false; bool is_writing = false; string not_processed_data = "";
    CancellationToken ct; CancellationTokenSource cts;
    public UART_Device(string UART_Id, double write_timeout_ms)
    {
        cdata = new data();
        cts = new CancellationTokenSource(); ct = cts.Token;
        connectAsync(UART_Id);
        device.BaudRate = 9600;
        device.Parity = SerialParity.None;
        device.StopBits = SerialStopBitCount.One;
        device.DataBits = 8;
        device.Handshake = SerialHandshake.None;
        reader = new DataReader(device.InputStream);
    }

    public string get_received_data(int index)
    {
        return cdata.rdata[index];
    }

    private int received_data_count()
    {
        lock (cdata)
        {
            return cdata.rdata.Count;
        }
    }

    public void clear_received_data()
    {
        lock (cdata)
        {
            cdata.rdata.Clear();
        }
    }

    public async void connectAsync(string UART_Id)
    {
        device = await SerialDevice.FromIdAsync(UART_Id);
    }

    public async void readAsync(double read_timeout_ms, double all_operation_time)
    {
        device.ReadTimeout = TimeSpan.FromMilliseconds(read_timeout_ms);
        cts.CancelAfter(TimeSpan.FromMilliseconds(all_operation_time));
        is_reading = true;
        while (true)
        {
            try
            {
                await readerAsync(ct);
            }
            catch (Exception) { }
        }
    }

    private async Task readerAsync(CancellationToken ct)
    {
        Task<UInt32> doerAsync;
        ct.ThrowIfCancellationRequested();
        reader.InputStreamOptions = InputStreamOptions.Partial;
        doerAsync = reader.LoadAsync(1024).AsTask(ct);
        UInt32 read_bytes_count = await doerAsync;
        not_processed_data += reader.ReadString(read_bytes_count);
        int newl_index = not_processed_data.IndexOf('\n');
        while (newl_index != -1)
        {
            lock (cdata)
            {
                cdata.rdata.Add(not_processed_data.Substring(0, newl_index + 1));
            }
            not_processed_data.Remove(0, newl_index + 1);
            newl_index = not_processed_data.IndexOf('\n');
        }
    }

    public async void writeAsync(string data_to_write, double write_timeout_ms, bool rewrite_on_error, int max_rewrite)
    {
        device.WriteTimeout = TimeSpan.FromMilliseconds(write_timeout_ms);
        writer = new DataWriter(device.OutputStream);
        await WriterAsync(data_to_write, rewrite_on_error, max_rewrite);
    }

    private async Task WriterAsync(string data, bool rewrite_on_error, int max_rewrite)
    {
        int length = data.Length;
        int occured_rewrites = 0;
        bool continue_to = true;
        Task<UInt32> doer;
        while (continue_to)
        {
            writer.WriteString(data);
            doer = writer.StoreAsync().AsTask(ct);
            UInt32 written_bytes_count = await doer;
            if (rewrite_on_error && written_bytes_count != length && occured_rewrites < max_rewrite)
            {
                continue_to = false;
                occured_rewrites++;
            }
        }
    }

    public void cancel_reading_immidiately()
    {
        lock (cts)
        {
            cts.Cancel(true);
        }
    }

    public void cancel_reading()
    {
        lock (cts)
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }
    }

    public void disconnect(bool wait_for_reading)
    {
        if (is_reading)
        {
            if (wait_for_reading)
            {
                cancel_reading();
            }
            else
            {
                cancel_reading_immidiately();
            }
        }
        device.Dispose();
    }
}