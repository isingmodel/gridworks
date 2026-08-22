using Gridworks.CommercialChecks;

try
{
    if (args.Length == 2 && args[0] == "--verify-batch")
    {
        CommercialGoldReplayVerifier.VerifyBatch(args[1], Console.OpenStandardOutput());
        return 0;
    }
    if (args.Length == 2 && args[0] == "--emit-snapshot")
    {
        CommercialGoldReplayVerifier.EmitSnapshot(args[1], Console.OpenStandardOutput());
        return 0;
    }
    throw new ArgumentException(
        "usage: Gridworks.GoldReplayVerifier --verify-batch ABSOLUTE_INPUT_JSON | " +
        "--emit-snapshot ABSOLUTE_INPUT_JSON");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL gold replay verifier: {exception.Message}");
    Console.Error.WriteLine(exception);
    return 1;
}
