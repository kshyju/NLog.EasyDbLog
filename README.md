## NLog.EasyDbLog
NLog target for easy db logging.

Setting up database logging with Nlog was a bit hard. So i created this nlog adapter/target to make it real easy. I did not care much about customizing layout as it is going to a db table and we can render it any way we want. 

1. Simply drop the attached dll to your bin folder. If you prefer to add a normal assembly reference, you can do that as well, but not necessary.
2. Add a new NLog  target to your NLog.config file for the db logger and use that as your logger.

As below.
```````
<targets>
 <target name="dbLogger" type="EasyDbLoggerTarget" ApplicationName="KpiGen" ConnectionStringName="NLogDb"/>
</targets>
<rules>    
     <logger name="*" levels="Warn,Debug,Trace,Error,Fatal" writeTo="dbLogger" />
</rules>
````````
Change the ApplicationName value to whatever app you are working on.

3. Specify the connection string so that the new logger can write to a SQL server database.


 `<add name="NLogDb" connectionString="Server=Power123;Database=Exceptions;User ID=a;Password=b" providerName="System.Data.SqlClient" />`

### Nuget

I also published this as a nuget if you do not like manually dropping the assembly

[https://www.nuget.org/packages/NLog.EasyDbLogger/](https://www.nuget.org/packages/NLog.EasyDbLogger/)


### Opserver compatability

The reason for creating this target was to throw all the error messages (from NLog) to the Exceptions table so that opserver can display it.

![alt Op server](https://raw.githubusercontent.com/kshyju/NLog.EasyDbLog/master/opserver-exceptions.png)

Big thanks to [https://github.com/NickCraver/StackExchange.Exceptional](https://github.com/NickCraver/StackExchange.Exceptional) as i copied a good amount of code from that as the whole idea was to write the exceptions to the same table.
