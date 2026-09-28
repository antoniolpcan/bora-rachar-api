using BoraRachar.Models;
using BoraRachar.Services;

namespace BoraRachar.Tests
{
    public class GroupServiceTests
    {
        [Fact]
        public async Task CreateAsync_WithValidData_SavesNormalizedGroup()
        {
            var repository = new RecordingGroupRepository();
            var service = new GroupService(repository);

            var result = await service.CreateAsync("  Viagem  ", new[] { " Antonio ", " Maria " });

            Assert.True(result.IsSuccess);

            var group = Assert.IsType<Group>(result.Group);

            Assert.Equal("Viagem", group.Name);
            Assert.Collection(
                group.Members,
                member => Assert.Equal("Antonio", member.Name),
                member => Assert.Equal("Maria", member.Name));

            Assert.All(
                group.Members,
                member => Assert.False(
                    string.IsNullOrWhiteSpace(member.Id)));

            Assert.Equal(
                group.Members.Count,
                group.Members.Select(member => member.Id).Distinct().Count());

            Assert.Equal(1, repository.CreateCalls);
            Assert.Same(group, repository.SavedGroup);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" A ")]
        public async Task CreateAsync_WithInvalidName_DoesNotSave(
            string name)
        {
            var repository = new RecordingGroupRepository();
            var service = new GroupService(repository);

            var result = await service.CreateAsync(
                name,
                new[] { "Antonio", "Maria" });

            Assert.False(result.IsSuccess);
            Assert.Null(result.Group);
            Assert.Equal("Name", result.ErrorField);
            Assert.Equal(0, repository.CreateCalls);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task CreateAsync_WithTooFewMembers_DoesNotSave(
            int memberCount)
        {
            var repository = new RecordingGroupRepository();
            var service = new GroupService(repository);

            var members = Enumerable
                .Range(0, memberCount)
                .Select(index => $"Participante {index}")
                .ToArray();

            var result = await service.CreateAsync("Viagem", members);

            Assert.False(result.IsSuccess);
            Assert.Null(result.Group);
            Assert.Equal("Members", result.ErrorField);
            Assert.Equal(0, repository.CreateCalls);
        }

        [Fact]
        public async Task CreateAsync_WithBlankMemberName_DoesNotSave()
        {
            var repository = new RecordingGroupRepository();
            var service = new GroupService(repository);

            var result = await service.CreateAsync(
                "Viagem",
                new[] { "Antonio", " " });

            Assert.False(result.IsSuccess);
            Assert.Null(result.Group);
            Assert.Equal("Members", result.ErrorField);
            Assert.Equal(0, repository.CreateCalls);
        }

        [Theory]
        [InlineData(99, true)]
        [InlineData(100, true)]
        [InlineData(101, false)]
        public async Task CreateAsync_ValidatesNameAfterTrimming(int length, bool accepted)
        {
            var dto = new BoraRachar.DTOs.Group.CreateGroupDto
            {
                Name = " " + new string('A', length) + " ",
                Members = new() { "Antonio", "Maria" }
            };
            var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                dto, new System.ComponentModel.DataAnnotations.ValidationContext(dto), errors, true));

            var repository = new RecordingGroupRepository();
            var service = new GroupService(repository);
            var result = await service.CreateAsync(dto.Name, dto.Members);

            Assert.Equal(accepted, result.IsSuccess);
            Assert.Equal(accepted ? 1 : 0, repository.CreateCalls);
            if (accepted)
                Assert.Equal(new string('A', length), result.Group!.Name);
            else
                Assert.Equal("Name", result.ErrorField);
        }
        private sealed class RecordingGroupRepository : TestGroupRepository
        {
        }
    }
}
