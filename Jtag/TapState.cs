namespace Jtag;

public enum TapState
{
    TestLogicReset, RunTestIdle, SelectDrScan, CaptureDr, ShiftDr, Exit1Dr,
    PauseDr, Exit2Dr, UpdateDr, SelectIrScan, CaptureIr, ShiftIr, Exit1Ir,
    PauseIr, Exit2Ir, UpdateIr
}
