using System;
using System.Collections.Generic;

namespace Puppeteer.UnitTest.LoyaltyDomain
{
	// Didactic domain for the Paper 4 cross-actor labs — mirrors a production
	// loyalty/rewards scenario: a Seller confirms a purchase order, a Reaction
	// on the Seller's journal observes the purchase and tells the RewardEngine
	// to apply loyalty campaigns to the customer.
	//
	// State on RewardEngine and Campaign is INSTANCE-level — not static. The
	// receiver actor builds its state via PerformCommand scripts (one to
	// instantiate `loyalty = RewardEngine();`, then `loyalty.AddCampaign(...)`
	// for each campaign), keeping the journal as the single source of truth.
	// No static registry, no C# parameters threaded across boundaries.

	// Sender-side domain entity. The Seller has a single business method
	// `purchase(...)` whose journal entry is what the Reaction observes; the
	// Reaction's `.Causation.Continue(...)` body issues the cross-actor `tell`
	// to the RewardEngine. The method body itself is a no-op stub — for the
	// didactic lab we care about the method *signature* (so the pattern
	// `[s:Seller].purchase(...)` resolves) and the resulting journal line.
	public class Seller
	{
		public void purchase(string orderId, DateTime date, decimal amount, string customerId)
		{
		}
	}

	// A loyalty Campaign with a date threshold and a minimum-amount threshold.
	// `Applies(date, amount)` decides whether the campaign qualifies for the
	// given purchase, and `Reward(orderId, customerId)` records the rewarding
	// (incrementing a counter the lab inspects).
	public class Campaign
	{
		public string Id;
		public DateTime ValidFrom;
		public decimal MinAmount;
		public int RewardsApplied;

		public Campaign() { }

		public Campaign(string id, DateTime validFrom, decimal minAmount)
		{
			Id = id;
			ValidFrom = validFrom;
			MinAmount = minAmount;
			RewardsApplied = 0;
		}

		public bool Applies(DateTime date, decimal amount)
		{
			return date >= ValidFrom && amount >= MinAmount;
		}

		public void Reward(string orderId, string customerId)
		{
			RewardsApplied++;
		}
	}

	// The RewardEngine on the receiver side. State is instance-level and is
	// built up by DSL commands — `loyalty = RewardEngine();` then
	// `loyalty.AddCampaign('id', date, minAmount);`. The same `loyalty`
	// reference persists in the actor's symbol table across PerformCommand
	// calls so subsequent commands (the for-loop driven by an inbound tell)
	// see the campaigns added during setup.
	public class RewardEngine
	{
		private readonly List<Campaign> campaigns = new List<Campaign>();

		public RewardEngine() { }

		// Tells reference RewardEngine('id-here'); the id is logical (which
		// rewarder instance to talk to) and is recorded in the envelope, but
		// in this single-process didactic lab it is informational only.
		public RewardEngine(string id) { }

		// 3-arg AddCampaign keeps the DSL setup script flat:
		//     loyalty.AddCampaign('C-newcomer', 1/1/2020, 10);
		// rather than forcing the lab to first create a Campaign instance.
		public void AddCampaign(string id, DateTime validFrom, decimal minAmount)
		{
			campaigns.Add(new Campaign(id, validFrom, minAmount));
		}

		// Exposed to the DSL `foreach (c in loyalty.Campaigns())` loop on the
		// receiver side.
		public List<Campaign> Campaigns()
		{
			return campaigns;
		}

		// Read model for queries: how many rewards this engine has applied in
		// total. Lets a scenario confirm the business outcome with a query
		// (`print loyalty.TotalRewards() v;`) instead of inspecting the journal.
		public int TotalRewards()
		{
			int total = 0;
			foreach (Campaign c in campaigns) total += c.RewardsApplied;
			return total;
		}
	}
}
