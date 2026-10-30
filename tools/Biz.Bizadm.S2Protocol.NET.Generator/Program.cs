using Biz.Bizadm.S2Protocol.NET.Generator;

var upstream = Upstream.Load();

return args.FirstOrDefault() switch
{
    "fetch" => await FetchCommand.RunAsync(upstream),
    "verify" => VerifyCommand.Run(upstream, BuildOption(args)),
    "emit" => EmitCommand.Run(upstream, BuildOption(args)),
    _ => Usage(),
};

static int? BuildOption(string[] args)
{
    int index = Array.IndexOf(args, "--build");
    return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : null;
}

static int Usage()
{
    Console.Error.WriteLine("usage: Biz.Bizadm.S2Protocol.NET.Generator fetch | verify [--build NNNNN] | emit [--build NNNNN]");
    return 1;
}
