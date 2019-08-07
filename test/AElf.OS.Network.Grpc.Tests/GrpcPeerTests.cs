using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.OS.Network.Application;
using AElf.OS.Network.Grpc;
using AElf.OS.Network.Infrastructure;
using AElf.Types;
using Shouldly;
using Volo.Abp.Threading;
using Xunit;

namespace AElf.OS.Network
{
    public class GrpcPeerTests : GrpcNetworkTestBase
    {
        private IBlockchainService _blockchainService;
        private IAElfNetworkServer _networkServer;
        
        private IPeerPool _pool;
        private GrpcPeer _grpcPeer;
        private GrpcPeer _nonInterceptedPeer;

        public GrpcPeerTests()
        {
            _blockchainService = GetRequiredService<IBlockchainService>();
            _networkServer = GetRequiredService<IAElfNetworkServer>();
            _pool = GetRequiredService<IPeerPool>();

            _grpcPeer = GrpcTestPeerHelpers.CreateNewPeer();
            _grpcPeer.IsConnected = true;

            _nonInterceptedPeer = GrpcTestPeerHelpers.CreateNewPeer("127.0.0.1:2000", false);
            _nonInterceptedPeer.IsConnected = true;

            _pool.TryAddPeer(_grpcPeer);
        }

        public override void Dispose()
        {
            AsyncHelper.RunSync(() => _networkServer.StopAsync(false));
        }

        [Fact]
        public void EnqueueBlock_ShouldExecuteCallback_Test()
        {
            AutoResetEvent executed = new AutoResetEvent(false);
            
            NetworkException exception = null;
            bool called = false;
            _nonInterceptedPeer.EnqueueBlock(new BlockWithTransactions(), ex =>
            {
                exception = ex;
                called = true;
                executed.Set();
            });

            executed.WaitOne(TimeSpan.FromMilliseconds(1000));
            exception.ShouldBeNull();
            called.ShouldBeTrue();
        }
        
        [Fact]
        public void EnqueueTransaction_ShouldExecuteCallback_Test()
        {
            AutoResetEvent executed = new AutoResetEvent(false);
            
            NetworkException exception = null;
            var transaction = new Transaction();
            bool called = false;
            _nonInterceptedPeer.EnqueueTransaction(transaction, ex =>
            {
                exception = ex;
                called = true;
                executed.Set();
            });

            executed.WaitOne(TimeSpan.FromMilliseconds(1000));
            exception.ShouldBeNull();
            called.ShouldBeTrue();
        }
        
        [Fact]
        public void EnqueueAnnouncement_ShouldExecuteCallback_Test()
        {
            AutoResetEvent executed = new AutoResetEvent(false);
            
            NetworkException exception = null;
            bool called = false;
            _nonInterceptedPeer.EnqueueAnnouncement(new BlockAnnouncement(), ex =>
            {
                exception = ex;
                called = true;
                executed.Set();
            });

            executed.WaitOne(TimeSpan.FromMilliseconds(1000));
            exception.ShouldBeNull();
            called.ShouldBeTrue();
        }

        [Fact]
        public void GetRequestMetrics_Test()
        {
            var metrics = _grpcPeer.GetRequestMetrics();
            metrics.Count.ShouldBe(3);
            var dicKeys = metrics.Keys.ToList();
            dicKeys.ShouldContain("GetBlock");
            dicKeys.ShouldContain("GetBlocks");
            dicKeys.ShouldContain("Announce");
        }

        [Fact]
        public async Task RequestBlockAsync_Success_Test()
        {
            var block = await _grpcPeer.GetBlockByHashAsync(Hash.FromRawBytes(new byte[]{1,2,7}));
            block.ShouldBeNull();

            var blockHeader = await _blockchainService.GetBestChainLastBlockHeaderAsync();
            block = await _grpcPeer.GetBlockByHashAsync(blockHeader.GetHash());
            block.ShouldNotBeNull();
        }

        [Fact]
        public async Task GetBlocksAsync_Success_Test()
        {
            var chain = await _blockchainService.GetChainAsync();
            var genesisHash = chain.GenesisBlockHash;

            var blocks = await _grpcPeer.GetBlocksAsync(genesisHash, 5);
            blocks.Count.ShouldBe(5);
            blocks.Select(o => o.Height).ShouldBe(new long[] {2, 3, 4, 5, 6});

            var blockHash = Hash.Empty;
            blocks = await _grpcPeer.GetBlocksAsync(blockHash, 1);
            blocks.ShouldBe(new List<BlockWithTransactions>());
        }

        [Fact]
        public async Task GetNodesAsync_Test()
        {
            var nodeList = await _grpcPeer.GetNodesAsync();
            nodeList.Nodes.Count.ShouldBe(0);
        }
    }
}