using System.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayWidget.Domain;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;
public sealed class RestoreTests(TestDatabase source):IClassFixture<TestDatabase>
{
    [Fact] public async Task DumpRestoreAndCopiedKeysRecoverInventoryAndEncryptedRetryReceipt()
    {
        var root=Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;
        var folder=Path.Combine(root,".local","restore-rehearsal",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var keys=Path.Combine(folder,"keys");Directory.CreateDirectory(keys);
        var protection=DataProtectionProvider.Create(new DirectoryInfo(keys),b=>b.SetApplicationName("StayWidget.RecoveryTest"));
        Guid property;Guid owner;GuestInput input;Receipt receipt;
        await using(var db=source.Db())
        {
            var service=new BookingService(db,TimeProvider.System,protection);owner=await service.User("urn:restore",Guid.NewGuid().ToString(),"Synthetic owner");
            var p=await service.Setup(owner,"Recovery test cottage","000 000 000","UTC","Synthetic property terms for recovery rehearsal.","Cottage",2);property=p.Id;
            var u=await db.Units.SingleAsync(x=>x.PropertyId==p.Id);input=new(u.Id,u.OpenFrom.AddDays(2),u.OpenFrom.AddDays(4),2,"Synthetic guest","000 000 001",BookingService.Secret(),true,Email:"recovery@example.invalid");
            receipt=await service.Submit(property,input);await service.Confirm(owner,receipt.Id,0,100,true);
        }
        var binary=Environment.GetEnvironmentVariable("STAYWIDGET_PG_BIN")??Path.GetFullPath(Path.Combine(root,"../shop-organizer/.local/pgsql/bin"));
        async Task Run(string exe,string connection,params string[] args)
        {
            var c=new NpgsqlConnectionStringBuilder(connection);Assert.Equal("127.0.0.1",c.Host);Assert.Equal(55439,c.Port);Assert.Equal("staywidget_dev",c.Username);Assert.StartsWith("staywidget_test_",c.Database!);
            var start=new ProcessStartInfo(Path.Combine(binary,exe)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var a in new[]{"-h",c.Host!,"-p",c.Port.ToString(),"-U",c.Username!,"-d",c.Database!}.Concat(args))start.ArgumentList.Add(a);
            start.Environment["PGPASSWORD"]=c.Password;using var process=Process.Start(start)!;
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try{await process.WaitForExitAsync(limit.Token);}catch{process.Kill(true);throw;}
            await Task.WhenAll(stdout,stderr);Assert.True(process.ExitCode==0,"Disposable PostgreSQL backup/restore command failed; private diagnostics intentionally omitted.");
        }
        var dump=Path.Combine(folder,"synthetic.dump");await Run("pg_dump.exe",source.Connection,"-Fc","-f",dump);Assert.True(new FileInfo(dump).Length>0);
        var restoredKeys=Path.Combine(folder,"restored-keys");Directory.CreateDirectory(restoredKeys);foreach(var file in Directory.GetFiles(keys))File.Copy(file,Path.Combine(restoredKeys,Path.GetFileName(file)));
        Assert.NotEmpty(Directory.GetFiles(restoredKeys));
        var target=new TestDatabase();await target.InitializeAsync();
        try
        {
            await Run("pg_restore.exe",target.Connection,"--clean","--if-exists",dump);
            await using var db=target.Db();var service=new BookingService(db,TimeProvider.System,DataProtectionProvider.Create(new DirectoryInfo(restoredKeys),b=>b.SetApplicationName("StayWidget.RecoveryTest")));
            Assert.Equal(receipt,await service.Submit(property,input));Assert.Equal("Accepted",(await service.Status(receipt.Token!))!.State);
            var queued=await db.ConfirmationEmails.SingleAsync(x=>x.PropertyId==property);
            var recovered=DataProtectionProvider.Create(new DirectoryInfo(restoredKeys),b=>b.SetApplicationName("StayWidget.RecoveryTest")).CreateProtector("StayWidget.ConfirmationEmail.v1").Unprotect(queued.ProtectedMessage);
            Assert.Contains("recovery@example.invalid",recovered);Assert.Contains(receipt.ActionToken!,recovered);
            Assert.Single(await db.Allocations.Where(x=>x.PropertyId==property&&x.Active).ToListAsync());
            await Assert.ThrowsAsync<RuleViolation>(()=>service.ManualBooking(owner,Guid.NewGuid(),new(input.UnitId,input.Arrival,input.Departure,2,"Conflict","000 000 002",100,true)));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: synthetic database and key-copy restoration; encrypted retry receipt, private status, inventory overlap and migration history verified. No production data.\n");
        }
        finally{await target.DisposeAsync();}
    }
}
