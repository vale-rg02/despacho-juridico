function InfoCard({ icon: Icon, label, value }) {
  return (
    <div className="bg-card border border-border rounded-lg p-4 flex gap-3 items-start">
      <div className="mt-0.5 p-2 rounded-md bg-secondary">
        <Icon size={15} className="text-accent" />
      </div>
      <div>
        <p className="text-xs text-muted-foreground mb-0.5" style={{ fontFamily: "'DM Mono', monospace" }}>{label}</p>
        <p className="text-sm font-medium text-foreground">{value}</p>
      </div>
    </div>
  )
}

export default InfoCard