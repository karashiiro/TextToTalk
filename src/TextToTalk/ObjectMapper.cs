using System;
using System.Runtime.CompilerServices;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using LiteDB;

namespace TextToTalk;

public class ObjectMapper
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Register one-way SeString mapper
        BsonMapper.Global.RegisterType<SeString>(
            serialize: value => value.TextValue,
            deserialize: _ => throw new NotSupportedException());

        // Register one-way GameObject mapper
        BsonMapper.Global.RegisterType<IGameObject>(
            serialize: value => value.Name.TextValue,
            deserialize: _ => throw new NotSupportedException());
    }
}