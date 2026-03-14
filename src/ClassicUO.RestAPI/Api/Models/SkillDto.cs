using ClassicUO.Game.Data;

namespace ClassicUO.RestApi.Models
{
    internal sealed class SkillDto
    {
        public SkillDto(Skill skill)
        {
            Name = skill.Name;
            Index = skill.Index;
            Value = skill.Value;
            Base = skill.Base;
            Cap = skill.Cap;
            Lock = skill.Lock.ToString();
        }

        public string Name { get; }
        public int Index { get; }
        public float Value { get; }
        public float Base { get; }
        public float Cap { get; }
        public string Lock { get; }
    }
}
