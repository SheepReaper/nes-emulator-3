using System;

namespace Sheep.Emulation.Nes.Cpu;

internal sealed class CpuControllerPorts
{
    private readonly byte[] _controllerState = new byte[2];
    private readonly byte[] _controllerShift = new byte[2];
    private bool _controllerStrobe;
    private ulong _strobeWriteCpuClock;

    public void SetControllerState(int controller, byte buttons)
    {
        if ((uint)controller >= _controllerState.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(controller));
        }

        _controllerState[controller] = buttons;
        if (_controllerStrobe || _controllerShift[controller] == 0)
        {
            _controllerShift[controller] = buttons;
        }
    }

    public byte ReadController(int controller, byte openBus)
    {
        var value = (byte)(_controllerShift[controller] & 0x01);
        if (!_controllerStrobe)
        {
            _controllerShift[controller] = (byte)((_controllerShift[controller] >> 1) | 0x80);
        }
        return (byte)((openBus & 0xE0) | value);
    }

    public void WriteStrobe(byte value, Apu apu)
    {
        var newStrobe = (value & 0x01) != 0;
        if (newStrobe)
        {
            _controllerStrobe = true;
            _strobeWriteCpuClock = apu.CpuClock;
            _controllerShift[0] = _controllerState[0];
            _controllerShift[1] = _controllerState[1];
        }
        else
        {
            if (_controllerStrobe)
            {
                var pulseDuration = apu.CpuClock - _strobeWriteCpuClock;
                if (pulseDuration == 1 && (_strobeWriteCpuClock & 1) == 1)
                {
                    _controllerShift[0] = 0xFF;
                    _controllerShift[1] = 0xFF;
                }
            }
            _controllerStrobe = false;
        }
    }

    public byte Peek(int controller) => (byte)(_controllerState[controller] & 1);
}
