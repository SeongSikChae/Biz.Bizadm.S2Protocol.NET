using Biz.Bizadm.S2Protocol.NET.Generator;

var upstream = Upstream.Load();

return args.FirstOrDefault() switch
{
    "fetch" => await FetchCommand.RunAsync(upstream),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: Biz.Bizadm.S2Protocol.NET.Generator fetch");
    return 1;
}
